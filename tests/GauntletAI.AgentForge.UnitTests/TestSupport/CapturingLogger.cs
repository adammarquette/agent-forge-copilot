using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.UnitTests.TestSupport;

/// <summary>Captures every formatted log line an <see cref="ILogger{T}"/> consumer writes, for asserting on log content directly.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Lines.Add(formatter(state, exception));
}
