using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Chat;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.UnitTests.Api.Chat;

public sealed class ChatSessionCoordinatorTests
{
    private readonly IAgentOrchestrator _orchestrator = A.Fake<IAgentOrchestrator>();
    private readonly IConversationStateStore _conversationStore = A.Fake<IConversationStateStore>();
    private readonly IChatMessageOutbox _outbox = A.Fake<IChatMessageOutbox>();
    private readonly IScopedAccessTokenProvider _tokenProvider = A.Fake<IScopedAccessTokenProvider>();
    private readonly IScopedClinicianIdentityAccessor _clinicianIdentityAccessor = A.Fake<IScopedClinicianIdentityAccessor>();
    private readonly ChatSessionCoordinator _sut;
    private readonly PatientSessionContext _session = new("token-abc", "default", "123", "dr-jones");

    public ChatSessionCoordinatorTests() =>
        _sut = new ChatSessionCoordinator(_orchestrator, _conversationStore, _outbox, _tokenProvider, _clinicianIdentityAccessor);

    [Fact]
    public async Task RequestBriefAsync_ValidSession_SetsScopedAccessTokenBeforeCallingTheOrchestrator()
    {
        // The token must be in place before the orchestrator (and everything it calls
        // transitively down to the FHIR client) runs, not after - this is the mechanism that
        // gets the session's token onto outbound OpenEMR calls without ever touching the browser.
        string? tokenDuringOrchestratorCall = null;
        A.CallTo(() => _orchestrator.StartBriefAsync("default", "123", A<CancellationToken>._))
            .Invokes(() => tokenDuringOrchestratorCall = _tokenProvider.AccessToken)
            .Returns(Task.FromResult(new AgentTurnResult("brief text", ConversationState.Start("default", "123"), [], [])));

        await _sut.RequestBriefAsync("session-1", _session, CancellationToken.None);

        tokenDuringOrchestratorCall.Should().Be("token-abc");
    }

    [Fact]
    public async Task RequestBriefAsync_ValidSession_SetsScopedClinicianIdentityBeforeCallingTheOrchestrator()
    {
        // FR-AUTH-4: the clinician identity must be in scope before any tool call runs, so the
        // MCP audit log can attribute every access this turn makes to who actually made it.
        string? identityDuringOrchestratorCall = null;
        A.CallTo(() => _orchestrator.StartBriefAsync("default", "123", A<CancellationToken>._))
            .Invokes(() => identityDuringOrchestratorCall = _clinicianIdentityAccessor.ClinicianIdentity)
            .Returns(Task.FromResult(new AgentTurnResult("brief text", ConversationState.Start("default", "123"), [], [])));

        await _sut.RequestBriefAsync("session-1", _session, CancellationToken.None);

        identityDuringOrchestratorCall.Should().Be("dr-jones");
    }

    [Fact]
    public async Task RequestBriefAsync_OrchestratorReturnsAResult_SavesItsStateKeyedBySessionId()
    {
        var finalState = ConversationState.Start("default", "123") with
        {
            Messages = [LlmMessage.FromText(LlmRole.Assistant, "brief text")],
        };
        A.CallTo(() => _orchestrator.StartBriefAsync("default", "123", A<CancellationToken>._))
            .Returns(Task.FromResult(new AgentTurnResult("brief text", finalState, [], [])));

        await _sut.RequestBriefAsync("session-1", _session, CancellationToken.None);

        A.CallTo(() => _conversationStore.Save("session-1", finalState)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RequestBriefAsync_OrchestratorReturnsAResult_AppendsABriefMessageToTheOutboxAndReturnsIt()
    {
        A.CallTo(() => _orchestrator.StartBriefAsync("default", "123", A<CancellationToken>._))
            .Returns(Task.FromResult(new AgentTurnResult("brief text", ConversationState.Start("default", "123"), [], [])));
        var appended = new ChatMessage(1, "brief", """{"answer":"brief text"}""");
        A.CallTo(() => _outbox.Append("session-1", "brief", A<string>.That.Contains("brief text")))
            .Returns(appended);

        var result = await _sut.RequestBriefAsync("session-1", _session, CancellationToken.None);

        result.Should().Be(appended);
    }

    [Fact]
    public async Task AskFollowUpAsync_NoPriorStateSaved_StartsAFreshConversationScopedToTheSessionsPatient()
    {
        A.CallTo(() => _conversationStore.TryGet("session-1")).Returns(null);
        A.CallTo(() => _orchestrator.AskFollowUpAsync(A<ConversationState>._, "Is her INR therapeutic?", A<CancellationToken>._))
            .Returns(Task.FromResult(new AgentTurnResult("Yes.", ConversationState.Start("default", "123"), [], [])));

        await _sut.AskFollowUpAsync("session-1", _session, "Is her INR therapeutic?", CancellationToken.None);

        A.CallTo(() => _orchestrator.AskFollowUpAsync(
                A<ConversationState>.That.Matches(s => s.Site == "default" && s.PatientId == "123" && s.Messages.Count == 0),
                "Is her INR therapeutic?", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AskFollowUpAsync_PriorStateSaved_ResumesFromIt()
    {
        var priorState = ConversationState.Start("default", "123") with
        {
            Messages = [LlmMessage.FromText(LlmRole.User, "Is her INR therapeutic?")],
        };
        A.CallTo(() => _conversationStore.TryGet("session-1")).Returns(priorState);
        A.CallTo(() => _orchestrator.AskFollowUpAsync(priorState, "When was it drawn?", A<CancellationToken>._))
            .Returns(Task.FromResult(new AgentTurnResult("Last week.", priorState, [], [])));

        await _sut.AskFollowUpAsync("session-1", _session, "When was it drawn?", CancellationToken.None);

        A.CallTo(() => _orchestrator.AskFollowUpAsync(priorState, "When was it drawn?", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AskFollowUpAsync_OrchestratorReturnsAResult_AppendsAnAnswerMessageToTheOutbox()
    {
        A.CallTo(() => _conversationStore.TryGet("session-1")).Returns(null);
        A.CallTo(() => _orchestrator.AskFollowUpAsync(A<ConversationState>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new AgentTurnResult("Yes.", ConversationState.Start("default", "123"), [], [])));
        var appended = new ChatMessage(2, "answer", """{"answer":"Yes."}""");
        A.CallTo(() => _outbox.Append("session-1", "answer", A<string>._)).Returns(appended);

        var result = await _sut.AskFollowUpAsync("session-1", _session, "Is her INR therapeutic?", CancellationToken.None);

        result.Should().Be(appended);
    }

    [Fact]
    public async Task RequestBriefAsync_OrchestratorReturnsSafetyFlags_IncludesThemInTheOutboxPayload()
    {
        // ARCHITECTURE.md's request-flow sequence diagram sends safety flags to the BFF alongside
        // the brief, not just into a log line - this proves they actually reach the wire payload.
        var flag = new DomainConstraintFlag("inr-therapeutic-range", "INR out of range", [new ClinicalSourceRef("Observation", "1")]);
        A.CallTo(() => _orchestrator.StartBriefAsync("default", "123", A<CancellationToken>._))
            .Returns(Task.FromResult(new AgentTurnResult("brief text", ConversationState.Start("default", "123"), [flag], [])));
        string? capturedPayload = null;
        A.CallTo(() => _outbox.Append("session-1", "brief", A<string>._))
            .Invokes((string _, string _, string payload) => capturedPayload = payload)
            .Returns(new ChatMessage(1, "brief", "{}"));

        await _sut.RequestBriefAsync("session-1", _session, CancellationToken.None);

        capturedPayload.Should().Contain("inr-therapeutic-range");
        capturedPayload.Should().Contain("Observation/1");
    }

    [Fact]
    public async Task RequestBriefAsync_OrchestratorReturnsSuppressedClaims_IncludesThemInTheOutboxPayload()
    {
        // PRD.md Sec.13.1's "Claim can't be grounded" row: "suppressed items noted" - this proves a
        // suppressed claim actually reaches the wire payload alongside the (shorter) verified
        // answer, not just a log line, the same way safety flags do above.
        var suppressed = new SuppressedClaim("Her INR is 9.0.", "no citation");
        A.CallTo(() => _orchestrator.StartBriefAsync("default", "123", A<CancellationToken>._))
            .Returns(Task.FromResult(new AgentTurnResult("brief text", ConversationState.Start("default", "123"), [], [suppressed])));
        string? capturedPayload = null;
        A.CallTo(() => _outbox.Append("session-1", "brief", A<string>._))
            .Invokes((string _, string _, string payload) => capturedPayload = payload)
            .Returns(new ChatMessage(1, "brief", "{}"));

        await _sut.RequestBriefAsync("session-1", _session, CancellationToken.None);

        capturedPayload.Should().Contain("Her INR is 9.0.");
        capturedPayload.Should().Contain("no citation");
    }

    [Fact]
    public void Resume_Always_DelegatesToTheOutbox()
    {
        var messages = new List<ChatMessage> { new(3, "answer", "{}") };
        A.CallTo(() => _outbox.GetSince("session-1", 2)).Returns(messages);

        var result = _sut.Resume("session-1", 2);

        result.Should().BeSameAs(messages);
    }
}
