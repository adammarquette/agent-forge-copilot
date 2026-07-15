namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Provider seam for cross-encoder reranking (W2_ARCHITECTURE.md §5, W2-D7) — Cohere Rerank over REST behind
/// Refit, or any equivalent. Reorders the RRF-merged candidate pool by relevance to the query so only the
/// top grounded snippets reach the answer model. A REST dependency, not a language dependency; when it is
/// unavailable the hybrid retriever keeps the fused order rather than failing (§10 degradation).
/// </summary>
public interface IReranker
{
    /// <summary>Reorders <paramref name="documents"/> by relevance to <paramref name="query"/>, best first.</summary>
    /// <param name="query">The clinical question / claim to ground.</param>
    /// <param name="documents">Candidate documents to score (id + text).</param>
    /// <param name="topK">Maximum documents to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query, IReadOnlyList<RerankDocument> documents, int topK, CancellationToken cancellationToken);
}

/// <summary>A candidate presented to the reranker: a stable id and the text to score.</summary>
public sealed record RerankDocument(string Id, string Text);

/// <summary>A reranked candidate: the id and the reranker's relevance score (higher is better).</summary>
public sealed record RerankedCandidate(string Id, double Score);
