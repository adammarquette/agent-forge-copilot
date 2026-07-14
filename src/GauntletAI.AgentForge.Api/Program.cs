using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Agenda;
using GauntletAI.AgentForge.Api.Chat;
using GauntletAI.AgentForge.Api.Health;
using GauntletAI.AgentForge.Api.Launch;
using GauntletAI.AgentForge.Api.Patient;
using GauntletAI.AgentForge.Api.Security;
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
using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Api.Evidence;
using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Retrieval;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
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
// Daily Agenda (ARCHITECTURE.md §19) is an optional, additive feature - not every environment
// configures it yet (confirmed live: ValidateOnStart here crashed the whole host at startup for
// tests/deployments with no reason to set OpenEmrAgenda config, e.g. HealthEndpointReadinessTests).
// No ValidateOnStart, matching ObservabilityOptions' precedent below: the app must still boot and
// serve the existing single-patient flow without this configured. Validation still runs lazily
// the first time these options are actually resolved (AgendaLaunchService/AgendaRosterService),
// producing a clean error there rather than an unguarded NullReferenceException
// (AgendaOpenEmrOptions.Validate).
builder.Services.AddOptions<AgendaOpenEmrOptions>()
    .Bind(builder.Configuration.GetSection(AgendaOpenEmrOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<AgendaOptions>()
    .Bind(builder.Configuration.GetSection(AgendaOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<LlmProviderOptions>()
    .Bind(builder.Configuration.GetSection(LlmProviderOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<DataProtectionKeyRingOptions>()
    .Bind(builder.Configuration.GetSection(DataProtectionKeyRingOptions.SectionName))
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

// AuthHandler is Transient and injects IAccessTokenProvider (Scoped by registration) - but
// IHttpClientFactory constructs a named client's DelegatingHandler chain using its OWN internal
// handler-building scope, never the calling hub invocation's DI scope, so no HandlerLifetime value
// makes AuthHandler observe the token ChatSessionCoordinator set (confirmed live 2026-07-10: every
// real FHIR tool call failed FR-AUTH-1's "no token" check even after forcing frequent handler
// rebuilds - see #39). The actual fix lives in ScopedAccessTokenProvider itself: its storage is a
// static AsyncLocal, not a per-instance field, so it flows correctly through the real async call
// chain regardless of which DI scope constructed which object along the way. Nothing special is
// needed here as a result - plain AddRefitClient, default handler pooling and its connection-reuse
// benefit both intact.
builder.Services.AddRefitClient<IOpenEmrAuthApi>()
    .ConfigureHttpClient((sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value.BaseUrl))
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddRefitClient<IOpenEmrFhirApi>()
    .ConfigureHttpClient((sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value.BaseUrl))
    .AddHttpMessageHandler<AuthHandler>()
    .AddHttpMessageHandler<CorrelationIdHandler>()
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

// Week 2 (Multimodal Evidence Agent, W2_ARCHITECTURE.md) - additive and OPTIONAL: only wired when a
// database connection string is configured, so the host still boots for the Week 1 flows / tests without
// a database (matching the AgendaOptions "optional feature" precedent above, and the smoke-test rule that
// the app must boot with whatever config an environment actually sets).
var weekTwoEnabled = !string.IsNullOrWhiteSpace(
    builder.Configuration.GetSection("AgentForgeData")["ConnectionString"]);
if (weekTwoEnabled)
{
    builder.Services.AddAgentForgeData(builder.Configuration);
    builder.Services.AddAgentForgeDocuments();
    builder.Services.AddAgentForgeRetrieval();
    builder.Services.AddAgentForgeEvidenceAgent();
}

builder.Services.AddScoped<SmartLaunchService>();
builder.Services.AddScoped<AgendaLaunchService>();
builder.Services.AddScoped<ChatSessionCoordinator>();

// TimeProvider.System, not DateTimeOffset.UtcNow directly: gives AgendaRosterServiceTests a fake
// clock seam instead of a bespoke IClock (ARCHITECTURE.md §19.1's single-captured-"now" design).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAgendaPatientSummaryRunner, AgendaPatientSummaryRunner>();
builder.Services.AddScoped<AgendaRosterService>();
builder.Services.AddScoped<PatientContextService>();

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

// Read directly from configuration (not IOptions<BffOptions>) - this runs before
// builder.Build(), so the DI container isn't available to resolve options from yet.
var bffPathBase = builder.Configuration.GetSection(BffOptions.SectionName)[nameof(BffOptions.PathBase)]
    ?? string.Empty;

// Persist the DataProtection key ring to durable, shared storage. The session cookie below carries
// the pending SMART-launch state (state + PKCE verifier); the framework's default in-memory key ring
// is regenerated per process, so a redeploy or a second replica cannot decrypt a cookie an earlier
// process wrote - the launch callback then fails with "No pending SMART launch for this session".
// reference: gitlab (sidecar DataProtection persistence). Empty KeyRingPath keeps the in-memory
// default for local dev / unit tests; every deployed environment must set it to a mounted volume.
var dataProtectionOptions = builder.Configuration.GetSection(DataProtectionKeyRingOptions.SectionName)
    .Get<DataProtectionKeyRingOptions>() ?? new DataProtectionKeyRingOptions();
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(dataProtectionOptions.ApplicationName);
if (dataProtectionOptions.KeyRingPath.Length > 0)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionOptions.KeyRingPath));
}

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    if (bffPathBase.Length > 0)
    {
        // Behind the reverse proxy (agent-forge#22), the sidecar is first-party with OpenEMR *as long
        // as the whole SMART launch stays on the proxy host*. Lax then suffices and keeps its CSRF
        // protection. This requires the OpenEMR module's launch URL (agentforge_launch_uri) to point
        // at the proxy front door, not the sidecar's own Railway host - otherwise /launch and
        // /callback land on different domains and the session cookie is lost (reference: gitlab#67).
        options.Cookie.Path = bffPathBase;
        options.Cookie.SameSite = SameSiteMode.Lax;
    }
    else
    {
        // Root-hosted (today's production state): the chat SPA runs inside a cross-origin iframe
        // embedded by the OpenEMR module shim (ARCHITECTURE.md §9's request flow), so from the
        // browser's perspective every request this session cookie needs to ride along with is
        // "cross-site" (the top-level document is OpenEMR's origin, not this sidecar's).
        // SameSite=Lax/Strict would silently stop sending the cookie the moment that's true -
        // None+Secure is required here, not a hardening choice to relax. Known gap: browsers with
        // strict third-party-cookie blocking (e.g. Safari ITP) may still block this outright -
        // the reverse-proxy path above is the actual fix, not a Storage Access API workaround.
        options.Cookie.SameSite = SameSiteMode.None;
    }
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddSignalR();

var app = builder.Build();

if (weekTwoEnabled)
{
    // Deploy-time schema management (W2-D14): apply migrations and seed the guideline corpus once at
    // startup, before serving traffic. Guarded above so environments without a database still boot.
    await app.Services.MigrateAgentForgeDataAsync();
    await using var seedScope = app.Services.CreateAsyncScope();
    await seedScope.ServiceProvider.GetRequiredService<GuidelineCorpusSeeder>().SeedAsync(CancellationToken.None);
}

if (bffPathBase.Length > 0)
{
    // Must run before UseSession/UseStaticFiles/routing - nginx (agent-forge#22) terminates TLS
    // and proxies a subpath, so both need to happen before anything downstream reads the request
    // path or scheme.
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    });
    app.UsePathBase(bffPathBase);
}

app.UseSession();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapLaunchEndpoints();
app.MapAgendaLaunchEndpoints();
app.MapAgendaEndpoints();
app.MapPatientEndpoints();
if (weekTwoEnabled)
{
    app.MapEvidenceEndpoints();
}

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
