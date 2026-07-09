using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.UnitTests.TestSupport;

/// <summary>
/// Minimal <see cref="ISession"/> test double backed by a dictionary - avoids FakeItEasy's
/// out-parameter ceremony for a framework storage contract that has no real behavior to fake
/// (the same reasoning as <c>CapturingHttpMessageHandler</c>).
/// </summary>
public sealed class InMemoryTestSession(string id = "test-session-id") : ISession
{
    private readonly Dictionary<string, byte[]> _store = [];

    public bool IsAvailable => true;

    public string Id { get; } = id;

    public IEnumerable<string> Keys => _store.Keys;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Clear() => _store.Clear();

    public void Remove(string key) => _store.Remove(key);

    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
