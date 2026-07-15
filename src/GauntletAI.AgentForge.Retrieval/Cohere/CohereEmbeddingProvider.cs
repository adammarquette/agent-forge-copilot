using GauntletAI.AgentForge.Data;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Retrieval.Cohere;

/// <summary>
/// <see cref="IEmbeddingProvider"/> backed by Cohere <c>/v2/embed</c> (embed-v4.0, W2-D7). Tags each call with
/// the correct <c>input_type</c> (search_document vs search_query) and requests the migration-bound output
/// dimension. Batches inputs at Cohere's 96-per-call limit.
/// </summary>
public sealed class CohereEmbeddingProvider : IEmbeddingProvider
{
    // Cohere's documented maximum texts per embed call.
    private const int MaxBatchSize = 96;

    private readonly ICohereApi _api;
    private readonly CohereOptions _options;

    /// <summary>Creates the provider over the Cohere API client and its options.</summary>
    public CohereEmbeddingProvider(ICohereApi api, IOptions<CohereOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs, EmbeddingInputType inputType, CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var inputTypeValue = inputType == EmbeddingInputType.Query ? "search_query" : "search_document";
        var vectors = new List<float[]>(inputs.Count);

        for (var offset = 0; offset < inputs.Count; offset += MaxBatchSize)
        {
            var batch = inputs.Skip(offset).Take(MaxBatchSize).ToList();
            var response = await _api.EmbedAsync(
                new CohereEmbedRequest
                {
                    Model = _options.EmbedModel,
                    InputType = inputTypeValue,
                    Texts = batch,
                    OutputDimension = EmbeddingModel.Dimensions,
                },
                cancellationToken).ConfigureAwait(false);

            vectors.AddRange(response.Embeddings?.Vectors ?? []);
        }

        return vectors;
    }
}
