namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Provider seam for dense embeddings (W2_ARCHITECTURE.md §5, W2-D7) — mirrors <c>ILlmProvider</c>. Used at
/// two points with <b>different</b> input types: embedding the guideline corpus at seed time
/// (<see cref="EmbeddingInputType.Document"/>) and embedding the query for dense KNN
/// (<see cref="EmbeddingInputType.Query"/>). The asymmetry is load-bearing — Cohere v3+/v4 models only place
/// query and document vectors in a comparable space when each is tagged with the right input type, so getting
/// it wrong silently degrades recall. Returned vectors have <see cref="Data.EmbeddingModel.Dimensions"/>
/// components. When no provider is configured the hybrid retriever degrades to sparse-only (§10).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Embeds each input string into a dense vector, preserving input order.</summary>
    /// <param name="inputs">Texts to embed (a single query, or a batch of corpus chunks).</param>
    /// <param name="inputType">Whether these are corpus documents or a search query (Cohere <c>input_type</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs, EmbeddingInputType inputType, CancellationToken cancellationToken);
}

/// <summary>Which side of the retrieval the text belongs to (maps to the embedding provider's input-type flag).</summary>
public enum EmbeddingInputType
{
    /// <summary>A corpus chunk being indexed for later retrieval (Cohere <c>search_document</c>).</summary>
    Document,

    /// <summary>A search query being matched against the corpus (Cohere <c>search_query</c>).</summary>
    Query,
}
