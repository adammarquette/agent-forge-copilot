namespace MarqSpec.AgentForge.Data.Entities;

/// <summary>
/// Machine-readable citation metadata attached to a derived fact — the Week 2 citation contract
/// (W2_ARCHITECTURE.md §7). Owned by <see cref="DerivedFact"/>; not a table of its own.
/// </summary>
public sealed class Citation
{
    /// <summary>The kind of source this citation resolves to.</summary>
    public CitationSourceType SourceType { get; set; }

    /// <summary>Source identifier (FHIR resource id, DocumentReference id, or guideline doc id).</summary>
    public required string SourceId { get; set; }

    /// <summary>PDF page number or guideline section, when applicable.</summary>
    public string? PageOrSection { get; set; }

    /// <summary>Extraction field name or retrieval chunk id, when applicable.</summary>
    public string? FieldOrChunkId { get; set; }

    /// <summary>The asserted value or quoted snippet.</summary>
    public string? QuoteOrValue { get; set; }

    /// <summary>Normalized bounding box <c>[x, y, w, h]</c> for document-sourced facts; null otherwise (§7 overlay).</summary>
    public double[]? BoundingBox { get; set; }
}
