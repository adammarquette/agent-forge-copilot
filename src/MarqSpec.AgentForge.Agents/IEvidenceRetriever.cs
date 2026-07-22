namespace MarqSpec.AgentForge.Agents;

/// <summary>
/// The evidence-retriever worker's contract: hybrid retrieval over the clinical-guideline corpus
/// (W2_ARCHITECTURE.md §5). The implementation (pgvector dense + Postgres FTS sparse, RRF, rerank) lives in
/// the retrieval tier and requires the database; the supervisor depends only on this seam.
/// </summary>
public interface IEvidenceRetriever
{
    /// <summary>Returns the top grounded guideline snippets for <paramref name="query"/>.</summary>
    /// <param name="query">The clinical question / claim to ground.</param>
    /// <param name="topK">Maximum snippets to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<EvidenceSnippet>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken);
}

/// <summary>One retrieved guideline snippet with the metadata needed to cite it (W2_ARCHITECTURE.md §7).</summary>
public sealed record EvidenceSnippet
{
    /// <summary>Guideline document id the snippet came from.</summary>
    public required string DocumentId { get; init; }

    /// <summary>Section heading within the document.</summary>
    public required string Section { get; init; }

    /// <summary>Stable chunk id (for citation).</summary>
    public required string ChunkId { get; init; }

    /// <summary>The snippet text.</summary>
    public required string Text { get; init; }

    /// <summary>Fused relevance score (higher is better).</summary>
    public double Score { get; init; }
}
