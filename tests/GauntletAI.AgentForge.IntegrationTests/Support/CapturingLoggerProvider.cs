using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>
/// Captures every formatted log line the real host writes, so a test can assert a secret never
/// appeared in a log message - the "log" half of "a token never appears in any client-visible
/// payload/header/log" (Epic 3's acceptance criteria). Implements <see cref="ISupportExternalScope"/>
/// so the real host's single shared scope provider (every <c>BeginScope</c> call across every
/// category - <see cref="ISupportExternalScope"/> is how <c>LoggerFactory</c> wires that up) is
/// available here too, letting a test confirm the correlation-id scope (ENGINEERING_STANDARDS.md
/// §7) actually reaches a real log line, not just a specific logger's own message.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider? _scopeProvider;

    public ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            owner._scopeProvider?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            List<string> scopeParts = [];
            owner._scopeProvider?.ForEachScope(
                (scope, parts) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
                    {
                        parts.AddRange(pairs.Select(kv => $"{kv.Key}={kv.Value}"));
                    }
                },
                scopeParts);

            owner.Lines.Enqueue(scopeParts.Count > 0 ? $"{message} {string.Join(" ", scopeParts)}" : message);
        }
    }
}
