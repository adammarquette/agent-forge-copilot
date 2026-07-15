namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Provider seam for dense embeddings (W2_ARCHITECTURE.md §5, W2-D7) — mirrors <c>ILlmProvider</c>. Used at
/// two points: embedding the guideline corpus at seed time, and embedding the query for dense KNN. The
/// returned vectors must have <see cref="Data.EmbeddingModel.Dimensions"/> components (the migration-bound
/// column width). Implementations call an embeddings REST endpoint; when none is configured the hybrid
/// retriever degrades to sparse-only rather than failing (§10).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Embeds each input string into a dense vector, preserving input order.</summary>
    /// <param name="inputs">Texts to embed (a single query, or a batch of corpus chunks).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}
