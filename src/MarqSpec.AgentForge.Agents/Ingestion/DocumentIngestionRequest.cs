using MarqSpec.AgentForge.Data.Entities;

namespace MarqSpec.AgentForge.Agents.Ingestion;

/// <summary>
/// A request to ingest one document that has already been stored in OpenEMR. The front desk uploads through
/// OpenEMR's own Documents workflow; the module then hands the sidecar the document's content and its
/// OpenEMR <c>DocumentReference</c> id so it can extract and persist derived facts pre-visit
/// (W2_ARCHITECTURE.md §4). The sidecar never writes the source document — OpenEMR is authoritative for it.
/// </summary>
public sealed record DocumentIngestionRequest
{
    /// <summary>OpenEMR patient id the document belongs to.</summary>
    public required string PatientId { get; init; }

    /// <summary>The OpenEMR <c>DocumentReference</c> id the derived facts cite (source lineage).</summary>
    public required string DocumentReferenceId { get; init; }

    /// <summary>The document type (drives the extraction schema).</summary>
    public required ClinicalDocumentType DocumentType { get; init; }

    /// <summary>Raw document bytes, supplied on the ingest call.</summary>
    public required byte[] Content { get; init; }

    /// <summary>IANA media type of the content.</summary>
    public required string MediaType { get; init; }
}
