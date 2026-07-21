namespace MarqSpec.AgentForge.Agents;

/// <summary>
/// A structured click-to-source citation for one document-derived fact (FR-CITE-2, gitlab#96): the page and
/// normalized bounding box the fact was read from, so the client can highlight the exact region on the source
/// page. Surfaced on the evidence answer alongside the prose. <see cref="BoundingBox"/> is null when the
/// extractor reported no region — the overlay degrades to page-level (§7).
/// </summary>
/// <param name="FactId">Whitespace-free slug matching the answer's <c>[Lab/&lt;slug&gt;]</c> citation token, so a clicked token resolves to its region.</param>
/// <param name="Field">The extracted field / test name (e.g. "INR").</param>
/// <param name="Value">The extracted value, as printed.</param>
/// <param name="Page">1-based source page the fact was read from.</param>
/// <param name="BoundingBox">Normalized <c>[x, y, w, h]</c> region; null when absent (page-level fallback).</param>
/// <param name="Quote">Verbatim supporting text from the document.</param>
/// <param name="SourceDocumentId">OpenEMR <c>DocumentReference</c> id to fetch the source PDF from for the production overlay (gitlab#109); null for a document attached in-turn, where the client already holds the bytes it uploaded.</param>
public sealed record DocumentCitation(
    string FactId, string Field, string Value, int Page, double[]? BoundingBox, string? Quote, string? SourceDocumentId = null);
