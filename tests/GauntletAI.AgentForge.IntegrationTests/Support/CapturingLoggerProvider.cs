using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>
/// Captures every formatted log line the real host writes, so a test can assert a secret never
/// appeared in a log message - the "log" half of "a token never appears in any client-visible
/// payload/header/log" (Epic 3's acceptance criteria).
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Lines.Enqueue(formatter(state, exception));
    }
}
