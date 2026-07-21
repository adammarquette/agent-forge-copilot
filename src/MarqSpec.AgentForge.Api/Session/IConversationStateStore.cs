using MarqSpec.AgentForge.Agent;

namespace MarqSpec.AgentForge.Api.Session;

/// <summary>
/// Holds each browser session's in-progress <see cref="ConversationState"/> between hub calls,
/// keyed by the stable ASP.NET Core session id (not the SignalR connection id, which changes on
/// every reconnect - ENGINEERING_STANDARDS.md §12's idempotent-resume requirement depends on this).
/// </summary>
public interface IConversationStateStore
{
    /// <summary>Saves <paramref name="state"/> for <paramref name="sessionId"/>, replacing any prior state.</summary>
    void Save(string sessionId, ConversationState state);

    /// <summary>Returns the saved state for <paramref name="sessionId"/>, or <see langword="null"/> if none exists.</summary>
    ConversationState? TryGet(string sessionId);
}
