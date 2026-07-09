using GauntletAI.AgentForge.IntegrationTests.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Runs the real BFF host (Epic 10, PRD.md §13.1) with a deliberately unreachable OpenEMR base
/// URL, while the LLM provider config is real and valid - proving /ready's per-dependency
/// granularity ("fails when *any one* dependency is down"), not just "everything is broken".
/// Nothing here is mocked (tests/AGENTS.md): the OpenEMR health check still makes a real HTTP
/// call, it just fails against a real DNS/connection error rather than a fake result.
/// </summary>
public sealed class HealthEndpointQaFixture : WebApplicationFactory<global::Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var llm = new AnthropicQaFixture();

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = "https://deliberately-unreachable.agentforge-qa-fault-injection.invalid",
            ["OpenEmr:Site"] = "default",
            ["OpenEmr:ClientId"] = "qa-integration-test-client",
            ["OpenEmr:Scopes:0"] = "patient/Patient.read",
            ["Bff:PublicBaseUrl"] = "https://bff-integration-test.invalid",
            ["Llm:ApiKey"] = llm.Options.ApiKey,
            ["Llm:Model"] = llm.Options.Model,
            ["Llm:InputPricePerMillionTokensUsd"] = "0",
            ["Llm:OutputPricePerMillionTokensUsd"] = "0",
        }));
    }
}
