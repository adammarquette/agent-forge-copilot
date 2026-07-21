using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.UnitTests.TestSupport;

/// <summary>
/// Captures every formatted log line an <see cref="ILogger{T}"/> consumer writes, including any
/// active <see cref="BeginScope{TState}"/> key/value state (e.g. the correlation-id scope,
/// ENGINEERING_STANDARDS.md §7) appended the same way a real scope-aware formatter would - for
/// asserting on log content directly.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly LoggerExternalScopeProvider _scopeProvider = new();

    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        List<string> scopeParts = [];
        _scopeProvider.ForEachScope(
            (scope, parts) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    parts.AddRange(pairs.Select(kv => $"{kv.Key}={kv.Value}"));
                }
            },
            scopeParts);

        Lines.Add(scopeParts.Count > 0 ? $"{message} {string.Join(" ", scopeParts)}" : message);
    }
}
