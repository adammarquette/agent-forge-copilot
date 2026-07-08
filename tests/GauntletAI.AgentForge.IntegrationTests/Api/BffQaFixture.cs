using System.Net;
using GauntletAI.AgentForge.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
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
            ["Llm:ApiKey"] = _llmApiKey,
            ["Llm:Model"] = _llmModel,
            ["Llm:InputPricePerMillionTokensUsd"] = "0",
            ["Llm:OutputPricePerMillionTokensUsd"] = "0",
        }));

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, SeedSessionStartupFilter>();
            services.AddSingleton<ILoggerProvider>(CapturedLogs);
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
            $"&patientId={Uri.EscapeDataString(OpenEmr.Options.TestPatientId!)}";
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
