using System.Collections.Concurrent;
using GauntletAI.AgentForge.Agent;

namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// Single-instance in-memory <see cref="IConversationStateStore"/>, matching the sidecar's
/// single-instance deployment assumption (v1 scope - no cross-instance session affinity needed).
/// </summary>
public sealed class InMemoryConversationStateStore : IConversationStateStore
{
    private readonly ConcurrentDictionary<string, ConversationState> _bySessionId = new();

    /// <inheritdoc />
    public void Save(string sessionId, ConversationState state) => _bySessionId[sessionId] = state;

    /// <inheritdoc />
    public ConversationState? TryGet(string sessionId) =>
        _bySessionId.TryGetValue(sessionId, out var state) ? state : null;
}
