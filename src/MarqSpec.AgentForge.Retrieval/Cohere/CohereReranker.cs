using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.Retrieval.Cohere;

/// <summary>
/// <see cref="IReranker"/> backed by Cohere <c>/v2/rerank</c> (rerank-v3.5, W2-D7). A cross-encoder — no
/// embeddings involved. Cohere returns an <c>index</c> into the documents we sent plus a relevance score; this
/// maps those indices back to the stable chunk ids so the hybrid retriever can reorder its snippets.
/// </summary>
public sealed class CohereReranker : IReranker
{
    private readonly ICohereApi _api;
    private readonly CohereOptions _options;

    /// <summary>Creates the reranker over the Cohere API client and its options.</summary>
    public CohereReranker(ICohereApi api, IOptions<CohereOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query, IReadOnlyList<RerankDocument> documents, int topK, CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        var response = await _api.RerankAsync(
            new CohereRerankRequest
            {
                Model = _options.RerankModel,
                Query = query,
                Documents = [.. documents.Select(d => d.Text)],
                TopN = topK,
            },
            cancellationToken).ConfigureAwait(false);

        var results = response.Results ?? [];
        return
        [
            .. results
                .Where(r => r.Index >= 0 && r.Index < documents.Count)
                .Select(r => new RerankedCandidate(documents[r.Index].Id, r.RelevanceScore))
        ];
    }
}
