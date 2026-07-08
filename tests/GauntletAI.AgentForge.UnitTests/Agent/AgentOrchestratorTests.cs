using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Agent;

public sealed class AgentOrchestratorTests
{
    private readonly ILlmProvider _llmProvider = A.Fake<ILlmProvider>();
    private readonly IMcpToolDispatcher _toolDispatcher = A.Fake<IMcpToolDispatcher>();
    private readonly AgentOrchestrator _sut;

    public AgentOrchestratorTests() =>
        _sut = new AgentOrchestrator(_llmProvider, _toolDispatcher, NullLogger<AgentOrchestrator>.Instance);

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
    public async Task StartBriefAsync_ExceedsMaxToolCallRounds_ThrowsRatherThanLoopingForever()
    {
        var call = new LlmToolCall("call_x", "get_labs", "{}");
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse(string.Empty, [call], LlmStopReason.ToolUse, new LlmUsage(1, 1, 0m))));
        A.CallTo(() => _toolDispatcher.DispatchAsync(A<string>._, A<string>._, A<LlmToolCall>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmToolResultContent("call_x", "{}")));

        var act = () => _sut.StartBriefAsync("default", "1", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartBriefAsync_Always_PassesCancellationTokenToTheLlmProvider()
    {
        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LlmResponse("ok", [], LlmStopReason.EndTurn, new LlmUsage(1, 1, 0m))));
        using var cts = new CancellationTokenSource();

        await _sut.StartBriefAsync("default", "1", cts.Token);

        A.CallTo(() => _llmProvider.CompleteAsync(A<LlmRequest>._, cts.Token)).MustHaveHappenedOnceExactly();
    }
}
