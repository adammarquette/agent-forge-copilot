using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Chat;
using GauntletAI.AgentForge.Api.Launch;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Integration.OpenEmr;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Llm.Anthropic;
using GauntletAI.AgentForge.Mcp;
using Microsoft.Extensions.Options;
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

// Per-request/per-hub-invocation scope: a fresh instance carries exactly one call's token and
// correlation id, so nothing here can leak between two different sessions or hub calls
// (ARCHITECTURE.md D11, §11).
builder.Services.AddScoped<ScopedAccessTokenProvider>();
builder.Services.AddScoped<IScopedAccessTokenProvider>(sp => sp.GetRequiredService<ScopedAccessTokenProvider>());
builder.Services.AddScoped<IAccessTokenProvider>(sp => sp.GetRequiredService<ScopedAccessTokenProvider>());
builder.Services.AddScoped<ICorrelationIdAccessor, MutableCorrelationIdAccessor>();

// Single-instance in-memory stores (v1 deployment assumption - see their own doc comments).
builder.Services.AddSingleton<IConversationStateStore, InMemoryConversationStateStore>();
builder.Services.AddSingleton<IChatMessageOutbox, InMemoryChatMessageOutbox>();

builder.Services.AddTransient<AuthHandler>();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddTransient<AnthropicAuthHandler>();

builder.Services.AddRefitClient<IOpenEmrAuthApi>()
    .ConfigureHttpClient((sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value.BaseUrl));

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
builder.Services.AddScoped<IMcpToolServer, McpToolServer>();
builder.Services.AddScoped<IMcpToolDispatcher, McpToolDispatcher>();
builder.Services.AddScoped<ILlmProvider, AnthropicLlmProvider>();
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

builder.Services.AddScoped<SmartLaunchService>();
builder.Services.AddScoped<ChatSessionCoordinator>();

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

app.Run();
