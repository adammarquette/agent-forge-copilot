namespace MarqSpec.AgentForge.Documents;

/// <summary>
/// Reads the words and their normalized rectangles from a digital PDF, for pixel-accurate citation boxes
/// (FR-CITE-2, gitlab#110). Implementations return an empty list for a scanned/image-only, encrypted, or
/// unreadable PDF, so the extractor degrades to the model's estimate rather than failing.
/// </summary>
public interface IPdfWordReader
{
    /// <summary>Reads every word (with a normalized top-left box) from the PDF; empty when none can be read.</summary>
    IReadOnlyList<PdfWord> ReadWords(ReadOnlyMemory<byte> pdf);
}
