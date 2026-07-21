using MarqSpec.AgentForge.Agents;

namespace MarqSpec.AgentForge.Retrieval;

/// <summary>
/// The dense (pgvector ANN over embeddings) half of hybrid retrieval (W2_ARCHITECTURE.md §5). Depends on
/// <see cref="IEmbeddingProvider"/> to embed the query; when embeddings are unavailable it returns empty so
/// <see cref="HybridEvidenceRetriever"/> degrades to sparse-only rather than failing (§10).
/// </summary>
public interface IDenseRetriever
{
    /// <summary>Returns up to <paramref name="topK"/> vector-nearest candidate snippets, best-first.</summary>
    Task<IReadOnlyList<EvidenceSnippet>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken);
}
