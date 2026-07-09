using GauntletAI.AgentForge.Integration.OpenEmr.Http;

namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// Mints the correlation id at BFF ingress (ARCHITECTURE.md §11): registered per-request/per-hub-
/// invocation scope, it lazily mints one value on first read and returns that same value for the
/// rest of the scope, so every downstream call within one request carries the same id.
/// </summary>
public sealed class MutableCorrelationIdAccessor : ICorrelationIdAccessor
{
    private string? _correlationId;

    /// <inheritdoc />
    public string CorrelationId => _correlationId ??= Guid.NewGuid().ToString("n");
}
