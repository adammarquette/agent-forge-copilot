namespace MarqSpec.AgentForge.Data.Entities;

/// <summary>
/// A patient document that has been ingested. The <b>source document is authoritative in OpenEMR</b>
/// (referenced by <see cref="OpenEmrDocumentReferenceId"/>); this row is the sidecar's index of it and the
/// parent of the derived facts (W2_ARCHITECTURE.md §4 data authority).
/// </summary>
public sealed class IngestedDocument
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Identifier of the patient the document belongs to.</summary>
    public required string PatientId { get; set; }

    /// <summary>Which document type was ingested.</summary>
    public ClinicalDocumentType DocumentType { get; set; }

    /// <summary>
    /// The OpenEMR <c>DocumentReference</c> id for the stored source document — the citation anchor and the
    /// authority link. Null only while the source write is still in flight.
    /// </summary>
    public string? OpenEmrDocumentReferenceId { get; set; }

    /// <summary>SHA-256 of the source file; unique, enforces idempotent ingestion (W2_ARCHITECTURE.md §4).</summary>
    public required string ContentHash { get; set; }

    /// <summary>When the document was ingested.</summary>
    public DateTimeOffset IngestedAt { get; set; }

    /// <summary>Facts derived from this document (sidecar-authoritative).</summary>
    public ICollection<DerivedFact> DerivedFacts { get; set; } = new List<DerivedFact>();
}
