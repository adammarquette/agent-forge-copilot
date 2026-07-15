using System.Text.Json.Serialization;
using Refit;

namespace GauntletAI.AgentForge.Retrieval.Cohere;

/// <summary>
/// Refit surface for the two Cohere v2 endpoints we use (W2-D7): <c>/v2/embed</c> (dense embeddings) and
/// <c>/v2/rerank</c> (cross-encoder rerank). A REST dependency behind a seam, not a language dependency. The
/// Bearer token is added by <c>CohereAuthHandler</c>, not here.
/// </summary>
public interface ICohereApi
{
    /// <summary>Embeds a batch of texts (max 96 per call).</summary>
    [Post("/v2/embed")]
    Task<CohereEmbedResponse> EmbedAsync([Body] CohereEmbedRequest request, CancellationToken cancellationToken);

    /// <summary>Reranks documents against the query, returning indices + relevance scores.</summary>
    [Post("/v2/rerank")]
    Task<CohereRerankResponse> RerankAsync([Body] CohereRerankRequest request, CancellationToken cancellationToken);
}

/// <summary>Cohere <c>/v2/embed</c> request body.</summary>
public sealed record CohereEmbedRequest
{
    /// <summary>Embedding model id.</summary>
    [JsonPropertyName("model")] public required string Model { get; init; }

    /// <summary><c>search_document</c> for corpus chunks, <c>search_query</c> for the query (the load-bearing asymmetry).</summary>
    [JsonPropertyName("input_type")] public required string InputType { get; init; }

    /// <summary>Texts to embed (≤ 96).</summary>
    [JsonPropertyName("texts")] public required IReadOnlyList<string> Texts { get; init; }

    /// <summary>Requested vector encodings; we take <c>float</c>.</summary>
    [JsonPropertyName("embedding_types")] public IReadOnlyList<string> EmbeddingTypes { get; init; } = ["float"];

    /// <summary>Output vector width (embed-v4.0 only); pinned to the migration-bound column width.</summary>
    [JsonPropertyName("output_dimension")] public int OutputDimension { get; init; }
}

/// <summary>Cohere <c>/v2/embed</c> response body.</summary>
public sealed record CohereEmbedResponse
{
    /// <summary>Embeddings keyed by encoding type.</summary>
    [JsonPropertyName("embeddings")] public CohereEmbeddings? Embeddings { get; init; }
}

/// <summary>The per-encoding embedding vectors.</summary>
public sealed record CohereEmbeddings
{
    /// <summary>Float vectors in input order (Cohere's <c>float</c> encoding key).</summary>
    [JsonPropertyName("float")] public IReadOnlyList<float[]>? Vectors { get; init; }
}

/// <summary>Cohere <c>/v2/rerank</c> request body.</summary>
public sealed record CohereRerankRequest
{
    /// <summary>Reranker model id.</summary>
    [JsonPropertyName("model")] public required string Model { get; init; }

    /// <summary>The search query.</summary>
    [JsonPropertyName("query")] public required string Query { get; init; }

    /// <summary>Candidate documents to score.</summary>
    [JsonPropertyName("documents")] public required IReadOnlyList<string> Documents { get; init; }

    /// <summary>Cap on returned results.</summary>
    [JsonPropertyName("top_n")] public int TopN { get; init; }
}

/// <summary>Cohere <c>/v2/rerank</c> response body.</summary>
public sealed record CohereRerankResponse
{
    /// <summary>Reranked results, best-first.</summary>
    [JsonPropertyName("results")] public IReadOnlyList<CohereRerankResult>? Results { get; init; }
}

/// <summary>One reranked result: an index into the request's <c>documents</c> plus its relevance score.</summary>
public sealed record CohereRerankResult
{
    /// <summary>Index into the original <c>documents</c> list.</summary>
    [JsonPropertyName("index")] public int Index { get; init; }

    /// <summary>Relevance score, normalized to [0, 1].</summary>
    [JsonPropertyName("relevance_score")] public double RelevanceScore { get; init; }
}
