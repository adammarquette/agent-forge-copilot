namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <summary>Terminal state of a document ingestion.</summary>
public enum DocumentIngestionStatus
{
    /// <summary>Extracted, source written, facts persisted.</summary>
    Ingested,

    /// <summary>The same content was already ingested (content-hash idempotency); nothing re-written.</summary>
    AlreadyIngested,

    /// <summary>Extraction failed the schema gate; nothing written or persisted ("vision without invention").</summary>
    ExtractionRejected,

    /// <summary>The source-document write to OpenEMR failed; nothing persisted (degrade, never fabricate).</summary>
    WriteFailed,
}

/// <summary>Outcome of a document ingestion. PHI-free.</summary>
public sealed record DocumentIngestionResult
{
    /// <summary>Terminal state.</summary>
    public required DocumentIngestionStatus Status { get; init; }

    /// <summary>Content-hash idempotency key, when computed.</summary>
    public string? ContentHash { get; init; }

    /// <summary>The OpenEMR <c>DocumentReference</c> id the facts cite; null when citation resolution is pending.</summary>
    public string? DocumentReferenceId { get; init; }

    /// <summary>True when the document was written but its citation could not yet be resolved (left pending).</summary>
    public bool CitationPending { get; init; }

    /// <summary>Number of derived facts persisted.</summary>
    public int FactCount { get; init; }

    /// <summary>Human-readable, PHI-free detail (e.g. a rejection or failure reason).</summary>
    public string? Detail { get; init; }

    /// <summary>Whether ingestion reached a persisted state (new or pre-existing).</summary>
    public bool Succeeded => Status is DocumentIngestionStatus.Ingested or DocumentIngestionStatus.AlreadyIngested;

    /// <summary>A newly ingested document.</summary>
    public static DocumentIngestionResult Ingested(string contentHash, string? documentReferenceId, int factCount) =>
        new()
        {
            Status = DocumentIngestionStatus.Ingested,
            ContentHash = contentHash,
            DocumentReferenceId = documentReferenceId,
            CitationPending = documentReferenceId is null,
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

    /// <summary>The source-document write failed.</summary>
    public static DocumentIngestionResult WriteFailed(string contentHash, string? detail) =>
        new() { Status = DocumentIngestionStatus.WriteFailed, ContentHash = contentHash, Detail = detail };
}
