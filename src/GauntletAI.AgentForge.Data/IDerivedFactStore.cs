using GauntletAI.AgentForge.Data.Entities;

namespace GauntletAI.AgentForge.Data;

/// <summary>
/// Persistence port for sidecar-authoritative ingestion records (W2_ARCHITECTURE.md §4). A thin adapter over
/// <see cref="AgentForgeDbContext"/>: the idempotency <i>decision</i> (check-then-write) lives in the
/// ingestion orchestrator so it stays unit-testable, while this port's real behavior — including the
/// content-hash unique constraint backstop — is exercised at the integration tier against Postgres.
/// </summary>
public interface IDerivedFactStore
{
    /// <summary>Returns the previously-ingested document with this content hash (with its facts), or null.</summary>
    Task<IngestedDocument?> FindByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);

    /// <summary>Persists a new ingested document and its derived facts in one save.</summary>
    Task AddAsync(IngestedDocument document, CancellationToken cancellationToken = default);
}
