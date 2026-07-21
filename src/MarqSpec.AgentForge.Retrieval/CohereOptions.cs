namespace MarqSpec.AgentForge.Retrieval;

/// <summary>
/// Configuration for the Cohere embedding + rerank provider (W2-D7). The API key is a secret and comes from
/// the environment / CI secret store (<c>Cohere__ApiKey</c>) — never source. When the key is absent the hybrid
/// retriever degrades to sparse-only (no dense retrieval, no rerank), so the app boots and serves without it.
/// </summary>
public sealed class CohereOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Cohere";

    /// <summary>Cohere API key; when empty the dense + rerank halves are disabled (sparse-only degradation).</summary>
    public string? ApiKey { get; init; }

    /// <summary>Cohere API base URL.</summary>
    public string BaseUrl { get; init; } = "https://api.cohere.com";

    /// <summary>Embedding model. <c>embed-v4.0</c> defaults to 1536-dim output — matching <c>EmbeddingModel.Dimensions</c>.</summary>
    public string EmbedModel { get; init; } = "embed-v4.0";

    /// <summary>Reranker (cross-encoder) model.</summary>
    public string RerankModel { get; init; } = "rerank-v3.5";

    /// <summary>Per-request timeout (seconds) for Cohere calls.</summary>
    public int RequestTimeoutSeconds { get; init; } = 30;

    /// <summary>True when a non-empty API key is configured; gates whether the live Cohere clients are wired.</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
}
