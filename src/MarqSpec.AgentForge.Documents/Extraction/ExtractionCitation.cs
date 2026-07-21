namespace MarqSpec.AgentForge.Documents.Extraction;

/// <summary>
/// Where in the source document an extracted fact came from — the Week 2 citation contract at extraction
/// time (W2_ARCHITECTURE.md §7). Every extracted fact must carry one, so an unsupported value cannot be
/// stated: this is the "vision without invention" gate. The OpenEMR <c>DocumentReference</c> id is attached
/// later at persistence; the model supplies the page and the verbatim supporting text.
/// </summary>
public sealed record ExtractionCitation
{
    /// <summary>1-based page number in the source document the value was read from.</summary>
    public required int Page { get; init; }

    /// <summary>Verbatim text from the document that supports the extracted value (no paraphrasing).</summary>
    public required string Quote { get; init; }

    /// <summary>Optional normalized bounding box <c>[x, y, w, h]</c> of the supporting region.</summary>
    public double[]? BoundingBox { get; init; }
}
