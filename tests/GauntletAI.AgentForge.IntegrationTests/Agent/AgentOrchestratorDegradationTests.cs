using FluentAssertions;
using GauntletAI.AgentForge.IntegrationTests.Llm;

namespace GauntletAI.AgentForge.IntegrationTests.Agent;

/// <summary>
/// Fault-injects real failures against the real <see cref="Agent.AgentOrchestrator"/> (Epic 10,
/// PRD.md §13.1) - a unit test faking <c>ILlmProvider</c> can prove the orchestrator's own
/// catch/degrade logic is correct, but not that a *real* exception type from the real Anthropic
/// client (a genuine <c>Refit.ApiException</c>, real network cancellation timing) actually reaches
/// that logic the way the unit tests assume. Nothing here is mocked (tests/AGENTS.md).
/// </summary>
[Trait("Metric", "M5-Degradation")]
public sealed class AgentOrchestratorDegradationTests : IClassFixture<AgentOrchestratorQaFixture>
{
    private readonly AgentOrchestratorQaFixture _fixture;

    public AgentOrchestratorDegradationTests(AgentOrchestratorQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task StartBriefAsync_RealLlmProviderRejectsAnInvalidKey_ReturnsADeterministicFallbackInsteadOfThrowing()
    {
        // PRD.md §13.1's "LLM timeout / provider error" row, against the real Anthropic API: an
        // invalid key produces a genuine 401 Refit.ApiException, not a fake one - this proves the
        // orchestrator's catch clause actually matches what the real client throws.
        var brokenProvider = AnthropicQaFixture.BuildProviderWithInvalidKey(_fixture.LlmOptions.Model);
        var orchestrator = _fixture.BuildOrchestrator(brokenProvider, TimeSpan.FromSeconds(60));

        var result = await orchestrator.StartBriefAsync(
            _fixture.OpenEmr.Options.Site, _fixture.OpenEmr.Options.TestPatientId!, CancellationToken.None);

        result.IsDeterministicFallback.Should().BeTrue();
        result.Answer.Should().NotBeNullOrEmpty("silence is never an acceptable failure mode - PRD.md §13.1");
    }

    [Fact]
    public async Task StartBriefAsync_TurnDeadlineExpiresBeforeTheRealApiCanRespond_ReturnsADeterministicFallback()
    {
        // Proves the deadline's linked CancellationToken actually cancels a real, in-flight HTTP
        // call against the real network promptly - a unit test with a fake Task.Delay can't prove
        // Refit/HttpClient honors cancellation against a real, uncontrolled-latency dependency. The
        // real, working provider is used here (not the invalid-key one above) so the only thing
        // that can stop this turn is the 1ms deadline itself.
        var realProvider = new AnthropicQaFixture().Provider;
        var orchestrator = _fixture.BuildOrchestrator(realProvider, TimeSpan.FromMilliseconds(1));

        var result = await orchestrator.StartBriefAsync(
            _fixture.OpenEmr.Options.Site, _fixture.OpenEmr.Options.TestPatientId!, CancellationToken.None);

        result.IsDeterministicFallback.Should().BeTrue();
    }
}
