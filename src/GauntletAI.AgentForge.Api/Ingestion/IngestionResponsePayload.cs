namespace GauntletAI.AgentForge.Api.Ingestion;

/// <summary>Response for the document-ingestion endpoint. PHI-free — status, the citable DocumentReference id
/// (when resolved), whether the citation is pending, the persisted fact count, and a reason.</summary>
public sealed record IngestionResponsePayload(
    string Status,
    string? DocumentReferenceId,
    bool CitationPending,
    int FactCount,
    string? Detail);
