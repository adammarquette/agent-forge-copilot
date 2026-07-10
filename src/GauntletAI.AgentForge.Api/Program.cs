using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Chat;
using GauntletAI.AgentForge.Api.Health;
using GauntletAI.AgentForge.Api.Launch;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Integration.OpenEmr;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Llm.Anthropic;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Observability;
using GauntletAI.AgentForge.Verification;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<OpenEmrOptions>()
    .Bind(builder.Configuration.GetSection(OpenEmrOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<BffOptions>()
    .Bind(builder.Configuration.GetSection(BffOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<LlmProviderOptions>()
    .Bind(builder.Configuration.GetSection(LlmProviderOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
// AgentOptions.TurnDeadline has a built-in default (Epic 10), so binding is optional - the app
// must still boot and use the default when this section is absent from configuration.
builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
// Optional self-hosted infra (observability/docker-compose.yml) - not required/ValidateOnStart,
// unlike OpenEmr/Llm above, since the app must still boot and serve traffic without it running.
builder.Services.AddOptions<ObservabilityOptions>()
    .Bind(builder.Configuration.GetSection(ObservabilityOptions.SectionName));

// Per-request/per-hub-invocation scope: a fresh instance carries exactly one call's token and
// correlation id, so nothing here can leak between two different sessions or hub calls
// (ARCHITECTURE.md D11, §11).
builder.Services.AddScoped<ScopedAccessTokenProvider>();
builder.Services.AddScoped<IScopedAccessTokenProvider>(sp => sp.GetRequiredService<ScopedAccessTokenProvider>());
builder.Services.AddScoped<IAccessTokenProvider>(sp => sp.GetRequiredService<ScopedAccessTokenProvider>());
builder.Services.AddScoped<ICorrelationIdAccessor, MutableCorrelationIdAccessor>();
builder.Services.AddScoped<ScopedClinicianIdentityAccessor>();
builder.Services.AddScoped<IScopedClinicianIdentityAccessor>(sp => sp.GetRequiredService<ScopedClinicianIdentityAccessor>());
builder.Services.AddScoped<IClinicianIdentityAccessor>(sp => sp.GetRequiredService<ScopedClinicianIdentityAccessor>());

// Single-instance in-memory stores (v1 deployment assumption - see their own doc comments).
builder.Services.AddSingleton<IConversationStateStore, InMemoryConversationStateStore>();
builder.Services.AddSingleton<IChatMessageOutbox, InMemoryChatMessageOutbox>();

builder.Services.AddTransient<AuthHandler>();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddTransient<AnthropicAuthHandler>();

// HandlerLifetime on both: AuthHandler and CorrelationIdHandler are Transient but inject Scoped
// state (the current call's access token / correlation id). IHttpClientFactory pools and reuses the
// underlying HttpMessageHandler chain for up to 2 minutes by default *across unrelated DI scopes* -
// whichever scope happens to trigger a given named client's handler construction gets its scoped
// state permanently captured into that pooled handler, and every other scope/session/hub call
// reusing it (this HttpClient's OpenEmrHealthCheck doesn't share this - it's a separate named
// client) silently reads the SAME stale value regardless of whose request it actually is. Confirmed
// live 2026-07-10: every real FHIR tool call failed FR-AUTH-1's "no token" check, not just some -
// the pooled handler had captured a scope where the token was never set.
//
// 1 second (SetHandlerLifetime's documented floor - TimeSpan.Zero throws ArgumentException at
// startup, confirmed the hard way) shrinks the window a stale scope can be reused from 2 minutes to
// 1 second; it is a mitigation, not a complete fix - two calls landing in the same 1s window can
// still share a handler built for a different scope. Tracked as a known gap in #39 pending the
// fully robust fix (token passed explicitly per Refit call, bypassing handler pooling for this
// dependency entirely) rather than risk a less-tested change under time pressure.
builder.Services.AddRefitClient<IOpenEmrAuthApi>()
    .ConfigureHttpClient((sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value.BaseUrl))
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .SetHandlerLifetime(TimeSpan.FromSeconds(1))
    .AddStandardResilienceHandler();

builder.Services.AddRefitClient<IOpenEmrFhirApi>()
    .ConfigureHttpClient((sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value.BaseUrl))
    .AddHttpMessageHandler<AuthHandler>()
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .SetHandlerLifetime(TimeSpan.FromSeconds(1))
    .AddStandardResilienceHandler();

builder.Services.AddRefitClient<IAnthropicMessagesApi>()
    .ConfigureHttpClient((sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value.BaseUrl))
    .AddHttpMessageHandler<AnthropicAuthHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddScoped<IOpenEmrAuthClient, OpenEmrAuthClient>();
builder.Services.AddScoped<IOpenEmrFhirClient, OpenEmrFhirClient>();

// AuditingMcpToolServer decorates the real tool server with the access-audit trail (FR-AUTH-4) -
// registered as IMcpToolServer so every consumer gets the audited version without knowing it.
builder.Services.AddScoped<McpToolServer>();
builder.Services.AddScoped<IMcpToolServer>(sp => new AuditingMcpToolServer(
    sp.GetRequiredService<McpToolServer>(),
    sp.GetRequiredService<IClinicianIdentityAccessor>(),
    sp.GetRequiredService<ICorrelationIdAccessor>(),
    sp.GetRequiredService<ILogger<AuditingMcpToolServer>>()));

builder.Services.AddScoped<IMcpToolDispatcher, McpToolDispatcher>();
builder.Services.AddScoped<ILlmProvider, AnthropicLlmProvider>();

// Stateless (no per-request data of their own), so a single shared instance is fine. Missing since
// Epic 7 first wired the verifier into AgentOrchestrator - the app never actually booted with that
// change in place until this was added; caught by a Program.cs smoke test during Epic 8's work.
builder.Services.AddSingleton<ISourceAttributionEngine, SourceAttributionEngine>();
builder.Services.AddSingleton(sp => new CardiologyConstraintEngine(
    CardiologyConstraintRules.Default, sp.GetRequiredService<ILogger<CardiologyConstraintEngine>>()));
builder.Services.AddSingleton<IClinicalResponseVerifier, ClinicalResponseVerifier>();

builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

builder.Services.AddScoped<SmartLaunchService>();
builder.Services.AddScoped<ChatSessionCoordinator>();

// Epic 9 (Observability): the app-side metrics/tracing that feed the self-hosted dashboard, and
// the readiness checks NFR-HEALTH-1 requires against OpenEMR, the LLM provider, and that dashboard's
// own backend. A single AgentForgeMetrics instance so every Counter/Histogram it owns aggregates
// across the whole process, not per-request.
builder.Services.AddSingleton<AgentForgeMetrics>();
builder.Services.AddSingleton<IAgentForgeMetrics>(sp => sp.GetRequiredService<AgentForgeMetrics>());

builder.Services.AddHttpClient<OpenEmrHealthCheck>();
builder.Services.AddHttpClient<LlmProviderHealthCheck>();
builder.Services.AddHttpClient<ObservabilityHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<OpenEmrHealthCheck>("openemr", tags: ["ready"])
    .AddCheck<LlmProviderHealthCheck>("llm-provider", tags: ["ready"])
    .AddCheck<ObservabilityHealthCheck>("observability", tags: ["ready"]);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("agentforge-api"))
    .WithMetrics(metrics => metrics
        .AddMeter(AgentForgeMetrics.MeterName)
        // Microsoft.Extensions.Http.Resilience's AddStandardResilienceHandler (the OpenEMR/LLM
        // clients above) already emits retry/circuit-breaker telemetry under this meter name - no
        // custom retry-counting code needed for the dashboard's "tool-call + retry counts" panel.
        .AddMeter("Polly")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        // Runtime above is GC/heap only - Process gives real process CPU%/RSS for Epic 12's
        // load-test baselines (PRD.md NFR-PERF-2/4), also useful ongoing since none of the other
        // instrumentation here surfaces true OS-level resource usage.
        .AddProcessInstrumentation()
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddSource(AgentForgeActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Correlation id (a logging scope - ChatSessionCoordinator, ENGINEERING_STANDARDS.md §7) actually
// rendered somewhere real; OTel's own log exporter, not a specific provider like Serilog/NLog, so
// the sidecar stays provider-agnostic per ENGINEERING_STANDARDS.md §7's sample-configs note.
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.AddConsoleExporter();
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    // The chat SPA runs inside a cross-origin iframe embedded by the OpenEMR module shim
    // (ARCHITECTURE.md §9's request flow), so from the browser's perspective every request this
    // session cookie needs to ride along with is "cross-site" (the top-level document is
    // OpenEMR's origin, not this sidecar's). SameSite=Lax/Strict would silently stop sending the
    // cookie the moment that's true - None+Secure is required, not a hardening choice to relax.
    // Known gap: browsers with strict third-party-cookie blocking (e.g. Safari ITP) may still
    // block this outright; a Storage Access API request from the iframe shim would be the fix,
    // and lives outside this repo (the shim is in the OpenEMR fork).
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddSignalR();

var app = builder.Build();

app.UseSession();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapLaunchEndpoints();
app.MapHub<ChatHub>("/hubs/chat");

// /health: liveness only (the process is up and serving) - no dependency checks, so it can't flap
// on a transient OpenEMR/LLM blip. /ready: the real NFR-HEALTH-1 checks (OpenEmrHealthCheck,
// LlmProviderHealthCheck, ObservabilityHealthCheck), tagged "ready" above.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapPrometheusScrapingEndpoint();

app.Run();

/// <summary>
/// Top-level statements generate an internal Program class; this partial declaration makes it
/// accessible to <c>WebApplicationFactory&lt;Program&gt;</c> from the integration test assembly
/// (standard ASP.NET Core testability convention - no behavior change).
/// </summary>
public partial class Program;
