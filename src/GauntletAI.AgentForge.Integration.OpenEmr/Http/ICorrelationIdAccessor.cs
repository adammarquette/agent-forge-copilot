namespace GauntletAI.AgentForge.Integration.OpenEmr.Http;

/// <summary>
/// Resolves the correlation id for the request currently in flight, so it can be propagated
/// onto every outbound OpenEMR call (FR-OBS-1, NFR-TRACE-1).
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>The correlation id minted at ingress for the current request.</summary>
    string CorrelationId { get; }
}
