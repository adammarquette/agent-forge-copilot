namespace MarqSpec.AgentForge.Data;

/// <summary>
/// Fixed parameters of the embedding space the guideline corpus is indexed in. The dimension is
/// <b>migration-bound</b>: it is baked into the <c>vector(N)</c> column, so changing it requires a new
/// migration and a re-embed of the corpus, not a runtime config change (W2_ARCHITECTURE.md §5, W2-D14).
/// </summary>
public static class EmbeddingModel
{
    /// <summary>
    /// Embedding vector dimensionality. Default 1536 (broadly compatible); fix to the chosen provider's
    /// output size before the first production migration (see the W2_ARCHITECTURE.md §16 [CONFIRM] item).
    /// </summary>
    public const int Dimensions = 1536;

    /// <summary>The PostgreSQL <c>vector</c> column type for <see cref="Dimensions"/> (e.g. <c>vector(1536)</c>).</summary>
    public static string ColumnType => $"vector({Dimensions})";
}
