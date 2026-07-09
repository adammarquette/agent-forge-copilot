using FluentAssertions;
using GauntletAI.AgentForge.Llm;

namespace GauntletAI.AgentForge.IntegrationTests.Agent;

/// <summary>
/// Exercises the full <see cref="GauntletAI.AgentForge.Agent.AgentOrchestrator"/> loop against the
/// real Anthropic API and the real QA OpenEMR deployment - the contract-drift coverage a unit test
/// faking <c>ILlmProvider</c> and <c>IMcpToolDispatcher</c> cannot provide: does the real model
/// actually request our tools, does the real dispatcher's result serialize back onto the wire in a
/// shape the real API accepts on the next round, and does an accumulated multi-turn history
/// (Epic 5's <c>LlmMessage</c> content-model extension) round-trip correctly. Kept to a small
/// number of calls since each turn is a real, billed API call that may itself take several
/// LLM round-trips.
/// </summary>
public sealed class AgentOrchestratorEndToEndTests : IClassFixture<AgentOrchestratorQaFixture>
{
    private readonly AgentOrchestratorQaFixture _fixture;

    public AgentOrchestratorEndToEndTests(AgentOrchestratorQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task StartBriefAsync_RealPatientAndRealLlm_DispatchesAtLeastOneRealToolAndReturnsFinalAnswer()
    {
        var result = await _fixture.Orchestrator.StartBriefAsync(
            _fixture.OpenEmr.Options.Site, _fixture.OpenEmr.Options.TestPatientId!, CancellationToken.None);

        result.Answer.Should().NotBeNullOrWhiteSpace();

        // Guards the actual round trip: the real model requested a real tool, the real dispatcher
        // executed it against real FHIR data, and every result came back without dispatcher-level
        // failure (an unknown tool, a malformed argument, or a downstream contract break would all
        // surface here as IsError) - none of which a unit test faking the dispatcher can prove.
        var toolResults = result.State.Messages
            .SelectMany(m => m.Content)
            .OfType<LlmToolResultContent>()
            .ToList();

        toolResults.Should().NotBeEmpty("the pre-visit brief requires at least one real tool call to ground its answer");
        toolResults.Should().OnlyContain(r => !r.IsError, "every real tool call against the configured test patient should succeed");
    }

    [Fact]
    public async Task AskFollowUpAsync_AfterBrief_RoundTripsAccumulatedToolHistoryAndReturnsCoherentAnswer()
    {
        // Guards the real-API compatibility of Epic 5's LlmMessage content-model extension:
        // a follow-up turn resends the entire prior history, including tool_use/tool_result blocks
        // from turn one, back to the real API. A unit test faking ILlmProvider can prove we built
        // that request shape; only the real API can prove it accepts it.
        var brief = await _fixture.Orchestrator.StartBriefAsync(
            _fixture.OpenEmr.Options.Site, _fixture.OpenEmr.Options.TestPatientId!, CancellationToken.None);

        var followUp = await _fixture.Orchestrator.AskFollowUpAsync(
            brief.State, "What allergies are on file for this patient?", CancellationToken.None);

        followUp.Answer.Should().NotBeNullOrWhiteSpace();
        followUp.State.Messages.Count.Should().BeGreaterThan(brief.State.Messages.Count);
    }
}
