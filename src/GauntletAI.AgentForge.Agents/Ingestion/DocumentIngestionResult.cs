namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <summary>Terminal state of a document ingestion.</summary>
public enum DocumentIngestionStatus
{
    /// <summary>Extracted and facts persisted.</summary>
    Ingested,

    /// <summary>The same content was already ingested (content-hash idempotency); nothing re-done.</summary>
    AlreadyIngested,

    /// <summary>Extraction failed the schema gate; nothing persisted ("vision without invention").</summary>
    ExtractionRejected,
}

/// <summary>Outcome of a document ingestion. PHI-free.</summary>
public sealed record DocumentIngestionResult
{
    /// <summary>Terminal state.</summary>
    public required DocumentIngestionStatus Status { get; init; }

    /// <summary>Content-hash idempotency key, when computed.</summary>
    public string? ContentHash { get; init; }

    /// <summary>The OpenEMR <c>DocumentReference</c> id the facts cite.</summary>
    public string? DocumentReferenceId { get; init; }

    /// <summary>Number of derived facts persisted.</summary>
    public int FactCount { get; init; }

    /// <summary>Human-readable, PHI-free detail (e.g. a rejection reason).</summary>
    public string? Detail { get; init; }

    /// <summary>Whether ingestion reached a persisted state (new or pre-existing).</summary>
    public bool Succeeded => Status is DocumentIngestionStatus.Ingested or DocumentIngestionStatus.AlreadyIngested;

    /// <summary>A newly ingested document.</summary>
    public static DocumentIngestionResult Ingested(string contentHash, string documentReferenceId, int factCount) =>
        new()
        {
            Status = DocumentIngestionStatus.Ingested,
            ContentHash = contentHash,
            DocumentReferenceId = documentReferenceId,
            FactCount = factCount,
        };

    /// <summary>The content was already on file.</summary>
    public static DocumentIngestionResult AlreadyIngested(string contentHash, string? documentReferenceId, int factCount) =>
        new()
        {
            Status = DocumentIngestionStatus.AlreadyIngested,
            ContentHash = contentHash,
            DocumentReferenceId = documentReferenceId,
            FactCount = factCount,
        };

    /// <summary>Extraction was rejected at the schema gate.</summary>
    public static DocumentIngestionResult ExtractionRejected(string contentHash, string? reason) =>
        new() { Status = DocumentIngestionStatus.ExtractionRejected, ContentHash = contentHash, Detail = reason };
}
