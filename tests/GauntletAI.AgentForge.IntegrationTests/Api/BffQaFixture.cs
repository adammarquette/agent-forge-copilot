using System.Net;
using GauntletAI.AgentForge.IntegrationTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Runs the real BFF host in-process against the real QA OpenEMR deployment and the real
/// Anthropic API - the only tier that can prove <see cref="global::Program"/>'s actual DI wiring,
/// session handling, and SignalR hub work end to end (tests/AGENTS.md - nothing here is mocked).
/// Reuses <see cref="OpenEmrQaFixture"/> and the <c>LlmQa__*</c> variables already configured for
/// Epic 4/6's QA tiers, mapped onto the BFF's own (differently-named) configuration sections via
/// in-memory config rather than requiring a second, parallel set of CI variables.
/// </summary>
public sealed class BffQaFixture : WebApplicationFactory<global::Program>
{
    /// <summary>
    /// Why every test that opens a <see cref="BuildHubConnection"/> is currently <c>Skip</c>ped.
    /// <see cref="ChatHub.OnConnectedAsync"/> throws <see cref="InvalidOperationException"/>
    /// ("Session has not been configured for this application or request") even on the very
    /// first, connection-establishing request - not just later long-polls. Confirmed NOT caused
    /// by middleware ordering (explicit <c>app.UseRouting()</c> before <c>app.UseSession()</c>
    /// made no difference) and NOT (only) the Secure-cookie-over-http mismatch fixed below
    /// (<c>PostConfigure&lt;SessionOptions&gt;</c> made the session cookie correctly arrive on the
    /// request - confirmed via a diagnostic dump of <c>Request.Cookies</c> - but
    /// <c>Features.Get&lt;ISessionFeature&gt;()</c> is still <see langword="null"/> on the
    /// <see cref="HttpContext"/> SignalR hands to the Hub). Forcing the in-memory
    /// <c>TestServer</c>'s base address to <c>https</c> (the standard workaround for Secure-cookie
    /// testing) was also tried and instead hung the connection indefinitely (confirmed via a real
    /// hang dump) for reasons not investigated further - don't retry that without digging into the
    /// hang first. This looks like a genuine SignalR + ASP.NET Core Session incompatibility under
    /// the <see cref="HttpTransportType.LongPolling"/> transport this fixture is forced to use
    /// (the in-memory <c>TestServer</c> has no real sockets, so WebSockets can't negotiate) -
    /// ASP.NET Core Session is fundamentally a per-HTTP-request abstraction, and SignalR's
    /// "current HttpContext for this connection" isn't guaranteed to be one that middleware ran
    /// against. <see cref="ChatHub"/> itself got a real, independently-justified, unit-tested fix
    /// alongside this (loads the session once in <c>OnConnectedAsync</c> instead of re-reading it
    /// per hub method call - see <c>ChatHubTests</c>), but that alone doesn't help here since
    /// <c>OnConnectedAsync</c> can't load a session that was never attached to begin with. Needs
    /// deeper investigation into SignalR's HttpContext/feature lifecycle under
    /// WebApplicationFactory + TestServer + LongPolling, or moving off <c>HttpContext.Session</c>
    /// inside the Hub entirely (e.g. session identity via the hub URL's query string at connect
    /// time, which reliably survives on <c>Context.GetHttpContext().Request.Query</c>).
    /// </summary>
    public const string ChatHubSessionSkipReason =
        "ChatHub.OnConnectedAsync throws - ASP.NET Core Session isn't available on the HttpContext " +
        "SignalR hands to the Hub under WebApplicationFactory + TestServer + LongPolling, even on " +
        "the connection-establishing request. Ruled out: middleware ordering, the Secure-cookie-over-" +
        "http mismatch (separately fixed), forcing https on TestServer (hangs instead). Needs deeper " +
        "investigation into SignalR's HttpContext/feature lifecycle under this transport, or moving " +
        "off HttpContext.Session inside the Hub entirely.";

    /// <summary>The underlying OpenEMR QA connection (base URL, site, test patient id/token).</summary>
    public OpenEmrQaFixture OpenEmr { get; }

    /// <summary>Every log line the running host has written, for the no-token-in-logs assertion.</summary>
    public CapturingLoggerProvider CapturedLogs { get; } = new();

    private readonly string _llmApiKey;
    private readonly string _llmModel;

    public BffQaFixture()
    {
        OpenEmr = new OpenEmrQaFixture();
        if (string.IsNullOrEmpty(OpenEmr.Options.TestAccessToken) || string.IsNullOrEmpty(OpenEmr.Options.TestPatientId))
        {
            throw new InvalidOperationException(
                $"BFF integration tests require {QaOpenEmrOptions.SectionName}__TestAccessToken and " +
                $"{QaOpenEmrOptions.SectionName}__TestPatientId - the token-custody and delivery tests need a " +
                "real authenticated session end to end (tests/AGENTS.md - nothing here is mocked).");
        }

        var llmConfig = new ConfigurationBuilder().AddEnvironmentVariables().Build().GetSection("LlmQa");
        var apiKey = llmConfig["ApiKey"];
        var model = llmConfig["Model"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                "BFF integration tests require LlmQa__ApiKey and LlmQa__Model environment variables - " +
                "the pre-visit brief exercised here calls the real Anthropic API, not a mock.");
        }

        _llmApiKey = apiKey;
        _llmModel = model;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = OpenEmr.Options.BaseUrl,
            ["OpenEmr:Site"] = OpenEmr.Options.Site,
            ["OpenEmr:ClientId"] = "qa-integration-test-client",
            ["OpenEmr:Scopes:0"] = "patient/patient.read",
            ["Bff:PublicBaseUrl"] = "https://bff-integration-test.invalid",
            // Daily Agenda (ARCHITECTURE.md §19) - its own registered client per
            // ScopeRepository::finalizeScopes silently dropping scopes outside a client's own
            // registration (INTERFACE_CONTROL.md A.4). Not yet a real registered QA client - see
            // agent-forge-copilot#56/agent-forge#19 - this only satisfies AgendaOpenEmrOptions'
            // ValidateOnStart() so the host still boots; agenda-launch tests that need a real
            // token exchange are deferred until that registration exists.
            ["OpenEmrAgenda:ClientId"] = "qa-integration-test-agenda-client",
            ["OpenEmrAgenda:Scopes:0"] = "user/Patient.read",
            ["Llm:ApiKey"] = _llmApiKey,
            ["Llm:Model"] = _llmModel,
            ["Llm:InputPricePerMillionTokensUsd"] = "0",
            ["Llm:OutputPricePerMillionTokensUsd"] = "0",
        }));

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, SeedSessionStartupFilter>();
            services.AddSingleton<ILoggerProvider>(CapturedLogs);

            // Program.cs requires Cookie.SecurePolicy = Always (the cross-origin iframe embedding
            // needs SameSite=None+Secure - see its own comment on why). The in-memory TestServer
            // this fixture runs on serves everything over http://, and CookieContainer correctly
            // (RFC 6265) refuses to re-attach a Secure cookie to an http request - the session
            // cookie set by SeedAuthenticatedSessionAsync would silently never reach the SignalR
            // hub's own requests. Relaxing just SecurePolicy here (production is untouched) is
            // the surgical fix; forcing the TestServer's base address to https instead hangs the
            // SignalR long-polling transport for reasons not worth chasing down further.
            services.PostConfigure<SessionOptions>(options => options.Cookie.SecurePolicy = CookieSecurePolicy.None);
        });
    }

    /// <summary>
    /// An <see cref="HttpClient"/> against the real in-memory host, carrying <paramref name="cookies"/>
    /// on every request and capturing every <c>Set-Cookie</c> it receives back into the same jar -
    /// shared with a <c>HubConnection</c>'s handler so both see the same session.
    /// </summary>
    public HttpClient CreateHttpClient(CookieContainer cookies) =>
        new(new CookieContainerHandler(cookies) { InnerHandler = Server.CreateHandler() }) { BaseAddress = Server.BaseAddress };

    /// <summary>
    /// Seeds a real, authenticated <c>PatientSessionContext</c> (the pre-obtained QA test token -
    /// see <see cref="Support.QaOpenEmrOptions.TestAccessToken"/>) via the test-only route, bypassing
    /// the interactive SMART login this automated test cannot drive. Returns the cookie jar carrying
    /// the resulting session cookie.
    /// </summary>
    public async Task<CookieContainer> SeedAuthenticatedSessionAsync(CancellationToken cancellationToken)
    {
        var cookies = new CookieContainer();
        using var client = CreateHttpClient(cookies);

        var query = $"?accessToken={Uri.EscapeDataString(OpenEmr.Options.TestAccessToken!)}" +
            $"&site={Uri.EscapeDataString(OpenEmr.Options.Site)}" +
            $"&patientId={Uri.EscapeDataString(OpenEmr.Options.TestPatientId!)}" +
            "&clinicianIdentity=qa-test-clinician";
        var response = await client.PostAsync(SeedSessionStartupFilter.SeedSessionPath + query, content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return cookies;
    }

    /// <summary>
    /// Builds a <see cref="HubConnection"/> to the real chat hub, carrying <paramref name="cookies"/>
    /// so the hub sees whatever session they hold. Forces long-polling: the in-memory TestServer
    /// transport has no real sockets, so WebSocket negotiation would fail.
    /// </summary>
    public HubConnection BuildHubConnection(CookieContainer cookies) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "/hubs/chat"), HttpTransportType.LongPolling, options =>
            {
                options.HttpMessageHandlerFactory = _ => new CookieContainerHandler(cookies) { InnerHandler = Server.CreateHandler() };
            })
            .Build();
}
