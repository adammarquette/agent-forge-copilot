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

    /// <summary>
    /// Returns all derived facts on file for a patient (newest first), each with its owning
    /// <see cref="IngestedDocument"/> loaded for the citation anchor. Empty when the patient has none. This is
    /// the read side of E2 (UC-6): the pre-visit brief surfaces facts ingested before the visit.
    /// </summary>
    Task<IReadOnlyList<DerivedFact>> GetByPatientAsync(string patientId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new ingested document and its derived facts in one save.</summary>
    Task AddAsync(IngestedDocument document, CancellationToken cancellationToken = default);
}
