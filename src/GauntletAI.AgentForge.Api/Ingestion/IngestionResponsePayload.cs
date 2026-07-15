namespace GauntletAI.AgentForge.Api.Ingestion;

/// <summary>Response for the document-ingestion endpoint. PHI-free — status, the cited DocumentReference id,
/// the persisted fact count, and a reason.</summary>
public sealed record IngestionResponsePayload(
    string Status,
    string? DocumentReferenceId,
    int FactCount,
    string? Detail);
