namespace MarqSpec.AgentForge.Retrieval.Cohere;

/// <summary>
/// No-op embedding provider registered when no Cohere key is configured: returns no vectors, so the dense
/// retriever produces nothing and the hybrid retriever degrades to sparse-only (§10). Keeps the app bootable
/// and the pipeline honest without a key.
/// </summary>
internal sealed class DisabledEmbeddingProvider : IEmbeddingProvider
{
    public Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs, EmbeddingInputType inputType, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<float[]>>([]);
}

/// <summary>
/// No-op reranker registered when no Cohere key is configured: returns nothing, so the hybrid retriever keeps
/// the RRF-fused order (§10 degradation) rather than reordering.
/// </summary>
internal sealed class DisabledReranker : IReranker
{
    public Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query, IReadOnlyList<RerankDocument> documents, int topK, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RerankedCandidate>>([]);
}
