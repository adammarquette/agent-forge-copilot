using FluentAssertions;

namespace GauntletAI.AgentForge.IntegrationTests.Agent;

/// <summary>
/// Epic 11 (Evaluation Suite, FR-EVAL-1): two PRD.md §13.1 boundary rows with zero prior coverage
/// anywhere in the suite - "Missing/incomplete patient data" (UC-5) and "Ambiguous query". Both
/// need the real model/verifier pipeline against real (genuinely sparse) data; a unit test's fake
/// tool results can't prove the real pipeline actually states a gap instead of fabricating, or
/// asks/interprets instead of silently guessing, rather than being coincidentally right on a
/// happy path.
/// </summary>
[Trait("Metric", "M1-Groundedness")]
[Trait("Metric", "M5-Degradation")]
public sealed class AgentOrchestratorBoundaryTests : IClassFixture<AgentOrchestratorQaFixture>
{
    private readonly AgentOrchestratorQaFixture _fixture;

    public AgentOrchestratorBoundaryTests(AgentOrchestratorQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task StartBriefAsync_TestPatientHasNoSeededClinicalData_NeverFabricatesAClaimToFillTheGap()
    {
        // The real QA test patient has no seeded labs/meds/problems (a known, pre-existing data
        // gap) - the honest behavior is to state that plainly, never invent a plausible-sounding
        // value. SourceAttributionEngine's verifier already suppresses any uncited clinical-value
        // claim, so the strongest, wording-independent signal that the model fabricated nothing is
        // that the verifier found nothing to suppress this turn.
        var result = await _fixture.Orchestrator.StartBriefAsync(
            _fixture.OpenEmr.Options.Site, _fixture.OpenEmr.Options.TestPatientId!, CancellationToken.None);

        result.Answer.Should().NotBeNullOrWhiteSpace();
        result.SuppressedClaims.Should().BeEmpty(
            "a fabricated claim against genuinely empty data would have no citation to back it and " +
            "would be caught and suppressed - finding none means nothing was fabricated in the first place");
    }

    [Fact]
    public async Task AskFollowUpAsync_GenuinelyAmbiguousQuestion_NeverSilentlyGuessesASpecificAnswer()
    {
        // PRD.md §13.1's "Ambiguous query" row: the model must ask one focused clarifying question
        // or state the interpretation it took - never guess silently. Exact wording varies too
        // much across real LLM calls to assert reliably (documented limitation, matching this
        // suite's existing honesty about heuristic checks - e.g. SourceAttributionEngine's own doc
        // comment), so this asserts the property that actually matters and *is* reliably checkable:
        // no ungrounded, specific clinical claim slipped through as if it were a confident answer.
        var brief = await _fixture.Orchestrator.StartBriefAsync(
            _fixture.OpenEmr.Options.Site, _fixture.OpenEmr.Options.TestPatientId!, CancellationToken.None);

        var result = await _fixture.Orchestrator.AskFollowUpAsync(
            brief.State, "Is she due for anything?", CancellationToken.None);

        result.Answer.Should().NotBeNullOrWhiteSpace();
        result.SuppressedClaims.Should().BeEmpty(
            "faced with genuine ambiguity (due for what - labs, a refill, an appointment?), a " +
            "silent guess at a specific clinical fact would be uncited and get suppressed - finding " +
            "none is consistent with the model asking for clarification or stating its interpretation " +
            "instead of guessing");
    }
}
