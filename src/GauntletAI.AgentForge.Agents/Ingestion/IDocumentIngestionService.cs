namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <summary>
/// Orchestrates document write-back (W2_ARCHITECTURE.md §4): content-hash idempotency → extract → snapshot →
/// write source to OpenEMR → resolve the DocumentReference → persist the derived facts with lineage. Each
/// failure degrades deterministically (nothing fabricated, nothing half-persisted) and cancellation
/// propagates.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>Ingests one document; see <see cref="DocumentIngestionResult"/> for outcomes.</summary>
    Task<DocumentIngestionResult> IngestAsync(DocumentIngestionRequest request, CancellationToken cancellationToken = default);
}
