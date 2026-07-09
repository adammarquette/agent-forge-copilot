using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.IntegrationTests.Support;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace GauntletAI.AgentForge.IntegrationTests.Mcp;

/// <summary>
/// Epic 11 (FR-EVAL-1)'s "Tool call fails / errors" row (PRD.md §13.1) - a unit test faking
/// <c>IOpenEmrFhirClient</c> can prove <see cref="McpToolDispatcher"/>'s catch-all is internally
/// correct, but not that it actually matches what a *real* downstream failure throws (a genuine
/// <c>Refit.ApiException</c>/<c>HttpRequestException</c> against an unreachable host), the same
/// reasoning <c>AgentOrchestratorDegradationTests</c> applies to a real LLM failure.
/// </summary>
[Trait("Metric", "M5-Degradation")]
public sealed class McpToolDispatcherDegradationTests : IClassFixture<McpToolServerQaFixture>
{
    private readonly McpToolServerQaFixture _fixture;

    public McpToolDispatcherDegradationTests(McpToolServerQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DispatchAsync_RealFhirServerUnreachable_ReturnsAGracefulErrorResultInsteadOfThrowing()
    {
        // A real, unreachable OpenEMR host (not a fake) - the exception type/shape a genuine DNS
        // or connection failure produces is what this proves the dispatcher actually handles.
        var brokenHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://deliberately-unreachable.agentforge-qa-fault-injection.invalid"),
        };
        var brokenFhirApi = RestService.For<IOpenEmrFhirApi>(brokenHttpClient);
        var brokenToolServer = new McpToolServer(
            new OpenEmrFhirClient(brokenFhirApi),
            new FixedCorrelationIdAccessor("qa-tool-degradation-test"),
            NullLogger<McpToolServer>.Instance);
        var dispatcher = new McpToolDispatcher(
            brokenToolServer, new AgentForgeMetrics(), NullLogger<McpToolDispatcher>.Instance);

        var toolCall = new LlmToolCall("call-1", "get_patient_summary", "{}");
        var result = await dispatcher.DispatchAsync(
            _fixture.OpenEmr.Options.Site, _fixture.OpenEmr.Options.TestPatientId ?? "irrelevant-patient-id",
            toolCall, CancellationToken.None);

        result.IsError.Should().BeTrue(
            "a real downstream connection failure must surface as a graceful tool-result error, never an unhandled exception " +
            "(PRD.md §13.1 - one tool failing degrades one step, not the whole turn)");
    }
}
