namespace GauntletAI.AgentForge.Documents;

/// <summary>
/// One word read from a digital PDF, with its rectangle normalized to the page — top-left origin, every
/// component in <c>[0,1]</c> — so it maps straight onto the click-to-source overlay (FR-CITE-2, gitlab#110).
/// </summary>
/// <param name="PageNumber">1-based page the word is on.</param>
/// <param name="Text">The word's text as the PDF's glyphs spell it.</param>
/// <param name="X">Left edge, fraction of page width.</param>
/// <param name="Y">Top edge, fraction of page height.</param>
/// <param name="Width">Fraction of page width.</param>
/// <param name="Height">Fraction of page height.</param>
public sealed record PdfWord(int PageNumber, string Text, double X, double Y, double Width, double Height);
