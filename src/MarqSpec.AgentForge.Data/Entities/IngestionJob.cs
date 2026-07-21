namespace MarqSpec.AgentForge.Data.Entities;

/// <summary>
/// Tracks one asynchronous document-ingestion job from enqueue to completion (W2_ARCHITECTURE.md §11.3).
/// </summary>
public sealed class IngestionJob
{
    /// <summary>Primary key (also the job id returned to callers).</summary>
    public Guid Id { get; set; }

    /// <summary>Patient the document belongs to.</summary>
    public required string PatientId { get; set; }

    /// <summary>Which document type is being ingested.</summary>
    public ClinicalDocumentType DocumentType { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public IngestionStatus Status { get; set; }

    /// <summary>Correlation id tying this job to the wider request trace (FR-OBS-1).</summary>
    public required string CorrelationId { get; set; }

    /// <summary>The resulting <see cref="IngestedDocument"/> id once completed; null until then.</summary>
    public Guid? IngestedDocumentId { get; set; }

    /// <summary>Failure detail when <see cref="Status"/> is <see cref="IngestionStatus.Failed"/>; PHI-free.</summary>
    public string? Error { get; set; }

    /// <summary>When the job was created / enqueued.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the job reached a terminal state; null while in progress.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
