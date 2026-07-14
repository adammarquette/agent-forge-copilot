using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Observability;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.UnitTests.Agent;

public sealed class AgentOrchestratorTests
{
    private readonly ILlmProvider _llmProvider = A.Fake<ILlmProvider>();
    private readonly IMcpToolDispatcher _toolDispatcher = A.Fake<IMcpToolDispatcher>();
    private readonly IClinicalResponseVerifier _verifier = A.Fake<IClinicalResponseVerifier>();
    private readonly IAgentForgeMetrics _metrics = A.Fake<IAgentForgeMetrics>();
    private readonly AgentOrchestrator _sut;

    public AgentOrchestratorTests()
    {
        // Pass-through by default: existing tests below assert on the raw LLM answer, so the
        // verifier must echo it back unchanged unless a test configures otherwise.
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .ReturnsLazily((string answer, IReadOnlyCollection<string> _) => new VerificationResult(true, answer, [], []));

        _sut = BuildSut(TimeSpan.FromSeconds(60));
    }

    // Every test below runs well under a minute, so a 60s default deadline never fires
    // incidentally - only the tests that deliberately configure a tiny deadline exercise it.
    private AgentOrchestrator BuildSut(TimeSpan turnDeadline) => new(
        _llmProvider, _toolDispatcher, _verifier, _metrics,
        Options.Create(new AgentOptions { TurnDeadline = turnDeadline }), NullLogger<AgentOrchestrator>.Instance);

    [Fact]
    public async Task StartBriefAsync_LlmAnswersImmediately_ReturnsAnswerWithoutDispatchingAnyTools()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("No active problems on file.", [], LlmStopReason.EndTurn, new LlmUsage(10, 5, 0.01m))));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("No active problems on file.");
        A.CallTo(_toolDispatcher).MustNotHaveHappened();
    }

    [Fact]
    public async Task StartBriefAsync_Always_OffersAllSixCatalogToolsAndTheCardiologySystemPrompt()
    {
        LlmRequest? captured = null;
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest req, CancellationToken _) => captured ??= req)
            .Returns(Task.FromResult(new LlmResponse("brief", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        captured!.SystemPrompt.Should().Be(CardiologyProfile.SystemPrompt);
        captured.Tools.Should().BeEquivalentTo(McpToolCatalog.AllTools);
    }

    [Fact]
    public async Task StartBriefAsync_LlmRequestsATool_DispatchesItAndFeedsResultBackBeforeFinalAnswer()
    {
        var toolCall = new LlmToolCall("call_1", "get_patient_summary", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [toolCall], LlmStopReason.ToolUse, new LlmUsage(10, 5, 0.01m)),
                new LlmResponse("Active problems: AFib.", [], LlmStopReason.EndTurn, new LlmUsage(20, 10, 0.02m)));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", toolCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_1", """{"problems":["AFib"]}""")));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("Active problems: AFib.");
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", toolCall, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_LlmRequestsMultipleToolsInOneTurn_DispatchesEveryOneOfThem()
    {
        var labsCall = new LlmToolCall("call_1", "get_labs", "{}");
        var vitalsCall = new LlmToolCall("call_2", "get_vitals", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [labsCall, vitalsCall], LlmStopReason.ToolUse, new LlmUsage(10, 5, 0.01m)),
                new LlmResponse("Labs and vitals reviewed.", [], LlmStopReason.EndTurn, new LlmUsage(20, 10, 0.02m)));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", labsCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_1", "{}")));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", vitalsCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_2", "{}")));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", labsCall, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", vitalsCall, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartAgendaSummaryAsync_LlmAnswersImmediately_ReturnsAnswerWithoutDispatchingAnyTools()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("Stable, no changes since last visit.", [], LlmStopReason.EndTurn, new LlmUsage(10, 5, 0.01m))));

        var result = await _sut.StartAgendaSummaryAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("Stable, no changes since last visit.");
        A.CallTo(_toolDispatcher).MustNotHaveHappened();
    }

    [Fact]
    public async Task StartAgendaSummaryAsync_Always_SendsADifferentUserPromptThanTheInRoomBrief()
    {
        // Guards against StartAgendaSummaryAsync ever silently reusing StartBriefAsync's prompt -
        // the agenda summary must stay short/list-friendly (ARCHITECTURE.md §19), distinct from
        // the fuller ~75-second in-room brief (USERS.md UC-1). A copy-paste that forgot to swap
        // the prompt constant would otherwise pass every other test in this file unnoticed.
        LlmRequest? capturedBrief = null;
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest req, CancellationToken _) => capturedBrief ??= req)
            .Returns(Task.FromResult(new LlmResponse("answer", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        LlmRequest? capturedAgenda = null;
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest req, CancellationToken _) => capturedAgenda ??= req)
            .Returns(Task.FromResult(new LlmResponse("answer", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        await _sut.StartAgendaSummaryAsync("default", "1", CancellationToken.None);

        var briefPromptText = ((LlmTextContent)capturedBrief!.Messages[0].Content[0]).Text;
        var agendaPromptText = ((LlmTextContent)capturedAgenda!.Messages[0].Content[0]).Text;
        agendaPromptText.Should().NotBe(briefPromptText);
    }

    [Fact]
    public async Task StartAgendaSummaryAsync_LlmRequestsATool_DispatchesItAndFeedsResultBackBeforeFinalAnswer()
    {
        // Confirms the agenda summary reuses the full tool-calling/verification turn loop, not a
        // stripped-down path.
        var toolCall = new LlmToolCall("call_1", "get_patient_summary", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [toolCall], LlmStopReason.ToolUse, new LlmUsage(10, 5, 0.01m)),
                new LlmResponse("Active problems: AFib.", [], LlmStopReason.EndTurn, new LlmUsage(20, 10, 0.02m)));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", toolCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_1", """{"problems":["AFib"]}""")));

        var result = await _sut.StartAgendaSummaryAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("Active problems: AFib.");
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", toolCall, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AskFollowUpAsync_ValidPriorState_SendsFullAccumulatedHistoryToTheLlm()
    {
        // Guards the "3-turn exchange resolves references without restating" acceptance
        // criterion at the code level: reference resolution itself is the model's job, but the
        // orchestrator must actually hand it the full history to resolve references *from* -
        // if history got dropped or truncated here, no amount of model capability would help.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("Her INR was 2.3, drawn last week.", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        var priorState = new ConversationState(
            "default",
            "1",
            [
                LlmMessage.FromText(LlmRole.User, "Is her INR therapeutic?"),
                LlmMessage.FromText(LlmRole.Assistant, "Yes, her most recent INR was 2.3."),
            ]);
        LlmRequest? captured = null;
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(new LlmResponse("Her INR was 2.3, drawn last week.", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));

        var result = await _sut.AskFollowUpAsync(priorState, "When was it drawn?", CancellationToken.None);

        captured!.Messages.Should().HaveCount(3);
        captured.Messages[0].Content.Should().ContainSingle().Which.Should().BeOfType<LlmTextContent>()
            .Which.Text.Should().Be("Is her INR therapeutic?");
        captured.Messages[2].Content.Should().ContainSingle().Which.Should().BeOfType<LlmTextContent>()
            .Which.Text.Should().Be("When was it drawn?");
        result.State.Messages.Should().HaveCount(4, "the new question and the new answer are both appended");
    }

    [Fact]
    public async Task AskFollowUpAsync_ChainedToolQuery_ResolvesPatientThenFetchesLabInSequentialRounds()
    {
        // Guards "a chained query (resolve patient -> fetch lab) produces a correct cited answer"
        // - two *sequential* tool rounds, the second depending on nothing from the first except
        // that the loop continued rather than stopping after round one.
        var summaryCall = new LlmToolCall("call_1", "get_patient_summary", "{}");
        var labsCall = new LlmToolCall("call_2", "get_labs", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [summaryCall], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m)),
                new LlmResponse(string.Empty, [labsCall], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m)),
                new LlmResponse("Her most recent INR was 2.3 (Observation/1).", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m)));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", summaryCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_1", "{}")));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", labsCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_2", """{"labs":[{"value":2.3}]}""")));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("Her most recent INR was 2.3 (Observation/1).");
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", summaryCall, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", labsCall, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_ExceedsMaxToolCallRounds_ReturnsADeterministicFallbackInsteadOfThrowing()
    {
        // PRD.md §13.1 / Epic 10 (NFR-REL-1): a misbehaving model that never stops calling tools
        // must still yield a bounded, honest result to the clinician - not an unhandled exception
        // that surfaces as a crash.
        var call = new LlmToolCall("call_x", "get_labs", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse(string.Empty, [call], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m))));
        A.CallTo(() => _toolDispatcher.DispatchAsync(A<string>._, A<string>._, A<LlmToolCall>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_x", """{"labs":[]}""")));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.IsDeterministicFallback.Should().BeTrue();
        result.Answer.Should().Contain("""{"labs":[]}""", "the raw tool data gathered so far must be handed back, never silently dropped");
    }

    [Fact]
    public async Task StartBriefAsync_CallerCancelsTheToken_TheTokenTheLlmProviderSeesAlsoBecomesCanceled()
    {
        // The orchestrator links the caller's token with an internal turn-deadline token (Epic
        // 10), so the LLM provider no longer receives the exact same CancellationToken value -
        // but caller cancellation must still reach it, which is the actual behavior this guards.
        // Cancels from inside the call itself (not after it returns): the linked token source is
        // scoped to the turn and gets disposed once it completes, so cancelling afterward
        // wouldn't prove anything about live propagation during the call.
        CancellationToken? capturedToken = null;
        using var cts = new CancellationTokenSource();
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest _, CancellationToken ct) =>
            {
                capturedToken = ct;
                cts.Cancel();
            })
            .Returns(Task.FromResult(new LlmResponse("ok", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));

        await _sut.StartBriefAsync("default", "1", cts.Token);

        capturedToken!.Value.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task StartBriefAsync_VerifierSuppressesTheDraft_ReturnsTheVerifiedAnswerNotTheRawOne()
    {
        // FR-VERIF-0: nothing bypasses the gate. If the raw LLM answer ever leaked through
        // instead of the verified one, this is the test that would catch it.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("Her INR is 9.0.", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        A.CallTo(() => _verifier.Verify("Her INR is 9.0.", A<IReadOnlyCollection<string>>._))
            .Returns(new VerificationResult(false, string.Empty, [new SuppressedClaim("Her INR is 9.0.", "no citation")], []));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.Answer.Should().BeEmpty();
        result.State.Messages[^1].Content.Should().ContainSingle().Which.Should().BeOfType<LlmTextContent>()
            .Which.Text.Should().BeEmpty("the stored history must match what actually shipped, not the suppressed draft");
    }

    [Fact]
    public async Task StartBriefAsync_VerifierSuppressesAClaim_SurfacesItOnTheResult()
    {
        // PRD.md Sec.13.1's "Claim can't be grounded" row: "suppressed items noted" - a suppressed
        // claim must reach the caller alongside the (now shorter) verified answer, not just vanish.
        var suppressed = new SuppressedClaim("Her INR is 9.0.", "no citation");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("Her INR is 9.0.", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Returns(new VerificationResult(false, string.Empty, [suppressed], []));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.SuppressedClaims.Should().ContainSingle().Which.Should().Be(suppressed);
    }

    [Fact]
    public async Task StartBriefAsync_VerifierReturnsConstraintFlags_SurfacesThemOnTheResult()
    {
        var flag = new DomainConstraintFlag("inr-therapeutic-range", "INR out of range", []);
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("Her INR is 4.0 [Observation/1].", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Returns(new VerificationResult(true, "Her INR is 4.0 [Observation/1].", [], [flag]));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.SafetyFlags.Should().ContainSingle().Which.Should().Be(flag);
    }

    [Fact]
    public async Task StartBriefAsync_MultipleToolRounds_PassesEveryRoundsToolResultJsonToTheVerifier()
    {
        var firstCall = new LlmToolCall("call_1", "get_patient_summary", "{}");
        var secondCall = new LlmToolCall("call_2", "get_labs", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [firstCall], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m)),
                new LlmResponse(string.Empty, [secondCall], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m)),
                new LlmResponse("Final answer.", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m)));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", firstCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_1", """{"a":1}""")));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", secondCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_2", """{"b":2}""")));
        IReadOnlyCollection<string>? capturedToolJson = null;
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Invokes((string _, IReadOnlyCollection<string> json) => capturedToolJson = json)
            .Returns(new VerificationResult(true, "Final answer.", [], []));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        capturedToolJson.Should().BeEquivalentTo(["""{"a":1}""", """{"b":2}"""]);
    }

    [Fact]
    public async Task StartBriefAsync_CompletesSuccessfully_RecordsAnAgentTurnMetricTaggedSuccess()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("ok", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _metrics.RecordAgentTurn(true, A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_ExceedsMaxToolCallRounds_RecordsAnAgentTurnMetricTaggedSuccess()
    {
        // A deterministic fallback is a graceful degradation, not a failure: the turn delivered
        // something honest and useful rather than crashing, so it counts as succeeded for
        // reliability metrics (PRD.md §13.1 - "partial-but-honest beats complete-but-untrustworthy").
        var call = new LlmToolCall("call_x", "get_labs", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse(string.Empty, [call], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m))));
        A.CallTo(() => _toolDispatcher.DispatchAsync(A<string>._, A<string>._, A<LlmToolCall>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_x", "{}")));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _metrics.RecordAgentTurn(true, A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_Always_RecordsLlmUsageFromTheResponse()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("ok", [], LlmStopReason.EndTurn, new LlmUsage(120, 45, 0.03m))));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _metrics.RecordLlmUsage(120, 45, 0.03m)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_LlmProviderThrowsBeforeAnyToolCalls_ReturnsADeterministicFallbackNotingNoDataAvailable()
    {
        // PRD.md §13.1's "LLM timeout / provider error" row: after the resilience pipeline
        // (Polly, wired at the HttpClient level) exhausts its retries and the call still fails,
        // the orchestrator must degrade to a deterministic answer rather than propagate the
        // exception - even when there is no tool data yet to fall back on.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new HttpRequestException("LLM provider unreachable"));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.IsDeterministicFallback.Should().BeTrue();
        result.Answer.Should().NotBeNullOrEmpty("silence is never an acceptable failure mode - PRD.md §13.1");
    }

    [Fact]
    public async Task StartBriefAsync_LlmProviderThrowsAfterAToolCallSucceeded_ReturnsADeterministicFallbackContainingTheRawToolData()
    {
        var summaryCall = new LlmToolCall("call_1", "get_patient_summary", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse(string.Empty, [summaryCall], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m))))
            .Once()
            .Then.ThrowsAsync(new HttpRequestException("LLM provider unreachable"));
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", summaryCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_1", """{"patient":"Ada Testpatient"}""")));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.IsDeterministicFallback.Should().BeTrue();
        result.Answer.Should().Contain("""{"patient":"Ada Testpatient"}""");
    }

    [Fact]
    public async Task StartBriefAsync_LlmProviderThrows_NeverRunsTheFallbackThroughTheVerifier()
    {
        // The fallback is raw tool JSON, not LLM-synthesized prose making claims - there is
        // nothing for FR-VERIF-0's citation gate to check, and running it through the verifier
        // (which expects [ResourceType/Id]-style prose citations) would only strip it to nothing.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new HttpRequestException("LLM provider unreachable"));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task StartBriefAsync_LlmProviderThrows_RecordsAnAgentTurnMetricTaggedSuccess()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new HttpRequestException("LLM provider unreachable"));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _metrics.RecordAgentTurn(true, A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_OperationCanceled_PropagatesRatherThanDegradingToFallback()
    {
        // Cancellation is not a degradation case - it means the caller stopped waiting, so it
        // must propagate as a cancellation, not get silently swallowed into a fallback "answer".
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.StartBriefAsync("default", "1", CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StartBriefAsync_CallersOwnCancellationTokenAlreadyCanceled_ThrowsRatherThanDegradingToFallback()
    {
        // Guards the deadline-vs-caller-cancellation distinction: a caller-driven cancellation
        // must still surface as a genuine cancellation even though the turn deadline mechanism
        // now also races a CancellationTokenSource internally.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.StartBriefAsync("default", "1", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StartBriefAsync_TurnExceedsItsConfiguredDeadline_ReturnsADeterministicFallbackContainingTheRawToolData()
    {
        // PRD.md §13.1's "Tool slow / hits deadline" row + the "per-request deadline" design
        // default: a turn that runs past its configured budget must return the verified/gathered
        // core now rather than block indefinitely.
        var sut = BuildSut(TimeSpan.FromMilliseconds(1));
        var summaryCall = new LlmToolCall("call_1", "get_patient_summary", "{}");
        A.CallTo(() => _toolDispatcher.DispatchAsync("default", "1", summaryCall, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_1", """{"patient":"Ada Testpatient"}""")));
        // Round 1 succeeds and requests a tool; round 2's LLM call never completes within the
        // 1ms deadline configured above, so the deadline's token cancels it first.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse(string.Empty, [summaryCall], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m))))
            .Once()
            .Then.ReturnsLazily(async (LlmRequest _, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
                return new LlmResponse("should never get here", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m));
            });

        var result = await sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.IsDeterministicFallback.Should().BeTrue();
        result.Answer.Should().Contain("""{"patient":"Ada Testpatient"}""");
    }

    [Fact]
    public async Task StartBriefAsync_VerifierReturnsAResult_RecordsTheVerificationOutcome()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("Her INR is 9.0.", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Returns(new VerificationResult(false, string.Empty, [new SuppressedClaim("Her INR is 9.0.", "no citation")], []));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _metrics.RecordVerificationResult(false)).MustHaveHappenedOnceExactly();
    }

    // --- PRD.md §13.1 "unexpected / unparseable model output": reject -> one repair -> else fallback (#23) ---

    [Fact]
    public async Task StartBriefAsync_LlmReturnsEmptyFinalAnswer_RepairsOnceAndReturnsTheRepairedAnswer()
    {
        // The model finished (EndTurn, not a tool call) but produced no text at all - it doesn't
        // meet the final-answer contract, so it's rejected and one repair re-prompt is issued.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m)),
                new LlmResponse("Recovered brief. [Observation/1]", [], LlmStopReason.EndTurn, new LlmUsage(2, 2, 0m)));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("Recovered brief. [Observation/1]");
        result.IsDeterministicFallback.Should().BeFalse();
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_LlmReturnsTruncatedOutput_RejectsItAndRepairs()
    {
        // A MaxTokens stop means the answer was cut off mid-thought - unusable/untrustworthy for a
        // clinical brief (it may end mid-fact or mid-citation), so it's rejected and repaired
        // rather than shipped truncated.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse("Her INR is 2.3 and her potassium is", [], LlmStopReason.MaxTokens, new LlmUsage(1, 1, 0m)),
                new LlmResponse("Complete brief. [Observation/1]", [], LlmStopReason.EndTurn, new LlmUsage(2, 2, 0m)));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("Complete brief. [Observation/1]");
        result.IsDeterministicFallback.Should().BeFalse();
    }

    [Fact]
    public async Task StartBriefAsync_MalformedOutputPersistsAfterOneRepair_DegradesToDeterministicFallback()
    {
        // reject -> one repair -> still malformed -> deterministic fallback. The repair is bounded
        // to exactly one attempt (two LLM calls total, not an unbounded retry loop).
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse(string.Empty, [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.IsDeterministicFallback.Should().BeTrue();
        result.Answer.Should().NotBeNullOrEmpty("silence is never an acceptable failure mode - PRD.md §13.1");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_MalformedOutput_NeverRunsTheMalformedDraftThroughTheVerifier()
    {
        // The malformed-output gate sits upstream of FR-VERIF-0: an empty/truncated draft is
        // rejected before the verifier (a citation gate for real prose, not a structural validator)
        // ever sees it, so only the repaired answer reaches verification.
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m)),
                new LlmResponse("Recovered. [Observation/1]", [], LlmStopReason.EndTurn, new LlmUsage(2, 2, 0m)));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        A.CallTo(() => _verifier.Verify(string.Empty, A<IReadOnlyCollection<string>>._)).MustNotHaveHappened();
        A.CallTo(() => _verifier.Verify("Recovered. [Observation/1]", A<IReadOnlyCollection<string>>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartBriefAsync_MalformedOutput_RepairPromptRePromptsTheModelToRetry()
    {
        // The repair must actually re-prompt (append a corrective user instruction), not silently
        // re-call with identical context.
        var requests = new List<LlmRequest>();
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest req, CancellationToken _) => requests.Add(req))
            .ReturnsNextFromSequence(
                new LlmResponse(string.Empty, [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m)),
                new LlmResponse("Recovered. [Observation/1]", [], LlmStopReason.EndTurn, new LlmUsage(2, 2, 0m)));

        await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        var lastMessage = requests[1].Messages[^1];
        lastMessage.Role.Should().Be(LlmRole.User);
        ((LlmTextContent)lastMessage.Content[0]).Text.Should().Contain("complete");
    }

    [Fact]
    public async Task StartBriefAsync_WellFormedFinalAnswer_MakesNoRepairAttempt()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("Clean brief. [Observation/1]", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));

        var result = await _sut.StartBriefAsync("default", "1", CancellationToken.None);

        result.Answer.Should().Be("Clean brief. [Observation/1]");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
