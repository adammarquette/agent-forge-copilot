namespace MarqSpec.AgentForge.Data.Entities;

/// <summary>The clinical document types the ingestion pipeline can extract (W2_ARCHITECTURE.md §3).</summary>
public enum ClinicalDocumentType
{
    /// <summary>A scanned or digital laboratory-results PDF.</summary>
    LabPdf,

    /// <summary>A patient intake form uploaded at the front desk.</summary>
    IntakeForm,
}

/// <summary>Lifecycle state of an asynchronous document-ingestion job (W2_ARCHITECTURE.md §11.3).</summary>
public enum IngestionStatus
{
    /// <summary>Enqueued, not yet picked up by a worker.</summary>
    Pending,

    /// <summary>A worker is extracting structured facts from the source document.</summary>
    Extracting,

    /// <summary>Extraction completed and derived facts were persisted.</summary>
    Completed,

    /// <summary>Extraction failed; see <see cref="IngestionJob.Error"/>.</summary>
    Failed,
}

/// <summary>The kind of source a citation resolves to (W2_ARCHITECTURE.md §7 citation contract).</summary>
public enum CitationSourceType
{
    /// <summary>A FHIR resource read from OpenEMR.</summary>
    Fhir,

    /// <summary>A fact derived by extraction from an ingested document.</summary>
    Derived,

    /// <summary>A clinical-guideline chunk from the RAG corpus.</summary>
    Guideline,
}
