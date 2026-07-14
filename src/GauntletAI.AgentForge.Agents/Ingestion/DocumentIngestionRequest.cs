using GauntletAI.AgentForge.Data.Entities;

namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <summary>A request to ingest one source document: extract it, write the source to OpenEMR, and persist the
/// derived facts with lineage. Driven by the administrative upload path (W2_ARCHITECTURE.md §4/§11.3).</summary>
public sealed record DocumentIngestionRequest
{
    /// <summary>OpenEMR patient id the document belongs to.</summary>
    public required string PatientId { get; init; }

    /// <summary>The document type (drives the extraction schema).</summary>
    public required ClinicalDocumentType DocumentType { get; init; }

    /// <summary>Raw file bytes.</summary>
    public required byte[] Content { get; init; }

    /// <summary>IANA media type of the file.</summary>
    public required string MediaType { get; init; }

    /// <summary>Original file name.</summary>
    public required string FileName { get; init; }

    /// <summary>Optional encounter id to associate the document with.</summary>
    public string? EncounterId { get; init; }
}
