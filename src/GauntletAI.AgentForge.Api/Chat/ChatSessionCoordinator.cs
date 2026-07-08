using System.Text.Json;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Session;

namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>
/// The hub-independent core of a chat turn: sets the session's token in scope, runs the
/// orchestrator, persists the resulting conversation state, and appends the answer to the outbox.
/// Kept independent of <see cref="Microsoft.AspNetCore.SignalR.Hub"/> so it is directly
/// unit-testable; <see cref="ChatHub"/> is the thin layer that reads the session and pushes the
/// result to the caller.
/// </summary>
public sealed class ChatSessionCoordinator(
    IAgentOrchestrator orchestrator,
    IConversationStateStore conversationStore,
    IChatMessageOutbox outbox,
    IScopedAccessTokenProvider tokenProvider)
{
    /// <summary>Starts the pre-visit brief for <paramref name="session"/> and returns the message appended to the outbox.</summary>
    public async Task<ChatMessage> RequestBriefAsync(string sessionId, PatientSessionContext session, CancellationToken cancellationToken)
    {
        tokenProvider.AccessToken = session.AccessToken;

        var result = await orchestrator.StartBriefAsync(session.Site, session.PatientId, cancellationToken).ConfigureAwait(false);

        conversationStore.Save(sessionId, result.State);
        return outbox.Append(sessionId, "brief", JsonSerializer.Serialize(new ChatAnswerPayload(result.Answer)));
    }

    /// <summary>
    /// Asks a follow-up for <paramref name="session"/>, resuming the session's saved conversation
    /// state if one exists, or starting fresh (scoped to the same patient) if not.
    /// </summary>
    public async Task<ChatMessage> AskFollowUpAsync(
        string sessionId, PatientSessionContext session, string question, CancellationToken cancellationToken)
    {
        tokenProvider.AccessToken = session.AccessToken;

        var state = conversationStore.TryGet(sessionId) ?? ConversationState.Start(session.Site, session.PatientId);
        var result = await orchestrator.AskFollowUpAsync(state, question, cancellationToken).ConfigureAwait(false);

        conversationStore.Save(sessionId, result.State);
        return outbox.Append(sessionId, "answer", JsonSerializer.Serialize(new ChatAnswerPayload(result.Answer)));
    }

    /// <summary>Returns every message for <paramref name="sessionId"/> after <paramref name="lastSeenSequence"/>.</summary>
    public IReadOnlyList<ChatMessage> Resume(string sessionId, long lastSeenSequence) =>
        outbox.GetSince(sessionId, lastSeenSequence);
}
