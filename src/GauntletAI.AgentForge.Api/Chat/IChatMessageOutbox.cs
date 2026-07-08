namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>
/// Records every message sent to a session so a reconnecting client can replay anything it missed
/// - the "no silent drops" half of ENGINEERING_STANDARDS.md §12. Keyed by the stable session id,
/// not the SignalR connection id, since resume must work across a reconnect that gets a new
/// connection id.
/// </summary>
public interface IChatMessageOutbox
{
    /// <summary>Appends a new message for <paramref name="sessionId"/> and returns it, sequence assigned.</summary>
    ChatMessage Append(string sessionId, string kind, string payloadJson);

    /// <summary>Returns every message for <paramref name="sessionId"/> with a sequence greater than <paramref name="lastSeenSequence"/>.</summary>
    IReadOnlyList<ChatMessage> GetSince(string sessionId, long lastSeenSequence);
}
