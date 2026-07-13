namespace GauntletAI.AgentForge.Data.Entities;

/// <summary>
/// A source document in the clinical-guideline RAG corpus. Sidecar-authoritative and non-PHI
/// (W2_ARCHITECTURE.md §5, §9 data model).
/// </summary>
public sealed class GuidelineDocument
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable title of the guideline document.</summary>
    public required string Title { get; set; }

    /// <summary>Provenance / citation source (e.g. issuing body and year).</summary>
    public required string Source { get; set; }

    /// <summary>Corpus version this document was ingested under.</summary>
    public required string Version { get; set; }

    /// <summary>When the document was ingested into the corpus.</summary>
    public DateTimeOffset IngestedAt { get; set; }

    /// <summary>The retrievable chunks produced from this document.</summary>
    public ICollection<GuidelineChunk> Chunks { get; set; } = new List<GuidelineChunk>();
}
