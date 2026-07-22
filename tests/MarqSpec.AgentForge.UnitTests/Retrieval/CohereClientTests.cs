using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Retrieval;
using MarqSpec.AgentForge.Retrieval.Cohere;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.UnitTests.Retrieval;

/// <summary>
/// Unit tests for the Cohere provider mappings (W2-D7). Guarded behavior: the embedder tags requests with the
/// correct input-type (the load-bearing asymmetry) at the migration-bound dimension; the reranker maps
/// Cohere's document indices back to our stable chunk ids and filters out-of-range indices. The live HTTP is
/// faked — these assert our request/response mapping, not Cohere.
/// </summary>
public sealed class CohereClientTests
{
    private readonly ICohereApi _api = A.Fake<ICohereApi>();
    private static readonly IOptions<CohereOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new CohereOptions { ApiKey = "test-key" });

    private CohereEmbeddingProvider Embedder() => new(_api, Options);

    private CohereReranker Reranker() => new(_api, Options);

    [Fact]
    public async Task EmbedAsync_Query_TagsSearchQueryAtTheMigrationBoundDimension()
    {
        CohereEmbedRequest? captured = null;
        A.CallTo(() => _api.EmbedAsync(A<CohereEmbedRequest>._, A<CancellationToken>._))
            .Invokes((CohereEmbedRequest r, CancellationToken _) => captured = r)
            .Returns(new CohereEmbedResponse { Embeddings = new CohereEmbeddings { Vectors = [[0.1f, 0.2f]] } });

        var result = await Embedder().EmbedAsync(["is her INR therapeutic?"], EmbeddingInputType.Query, CancellationToken.None);

        captured!.InputType.Should().Be("search_query");
        captured.Model.Should().Be("embed-v4.0");
        captured.OutputDimension.Should().Be(MarqSpec.AgentForge.Data.EmbeddingModel.Dimensions);
        result.Should().ContainSingle().Which.Should().Equal(0.1f, 0.2f);
    }

    [Fact]
    public async Task EmbedAsync_Document_TagsSearchDocument()
    {
        CohereEmbedRequest? captured = null;
        A.CallTo(() => _api.EmbedAsync(A<CohereEmbedRequest>._, A<CancellationToken>._))
            .Invokes((CohereEmbedRequest r, CancellationToken _) => captured = r)
            .Returns(new CohereEmbedResponse { Embeddings = new CohereEmbeddings { Vectors = [[0.1f]] } });

        await Embedder().EmbedAsync(["a guideline chunk"], EmbeddingInputType.Document, CancellationToken.None);

        captured!.InputType.Should().Be("search_document");
    }

    [Fact]
    public async Task EmbedAsync_EmptyInputs_ReturnsEmptyWithoutCallingCohere()
    {
        var result = await Embedder().EmbedAsync([], EmbeddingInputType.Query, CancellationToken.None);

        result.Should().BeEmpty();
        A.CallTo(() => _api.EmbedAsync(A<CohereEmbedRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RerankAsync_MapsCohereIndicesBackToChunkIds_AndPassesTopN()
    {
        List<RerankDocument> docs = [new("id-A", "text a"), new("id-B", "text b"), new("id-C", "text c")];
        CohereRerankRequest? captured = null;
        A.CallTo(() => _api.RerankAsync(A<CohereRerankRequest>._, A<CancellationToken>._))
            .Invokes((CohereRerankRequest r, CancellationToken _) => captured = r)
            .Returns(new CohereRerankResponse { Results = [new() { Index = 2, RelevanceScore = 0.9 }, new() { Index = 0, RelevanceScore = 0.5 }] });

        var result = await Reranker().RerankAsync("q", docs, 2, CancellationToken.None);

        result.Select(r => r.Id).Should().Equal("id-C", "id-A");
        result[0].Score.Should().Be(0.9);
        captured!.TopN.Should().Be(2);
        captured.Query.Should().Be("q");
        captured.Model.Should().Be("rerank-v3.5");
    }

    [Fact]
    public async Task RerankAsync_DropsOutOfRangeIndices()
    {
        List<RerankDocument> docs = [new("id-A", "text a")];
        A.CallTo(() => _api.RerankAsync(A<CohereRerankRequest>._, A<CancellationToken>._))
            .Returns(new CohereRerankResponse { Results = [new() { Index = 5, RelevanceScore = 0.9 }, new() { Index = 0, RelevanceScore = 0.4 }] });

        var result = await Reranker().RerankAsync("q", docs, 5, CancellationToken.None);

        result.Select(r => r.Id).Should().Equal("id-A");
    }

    [Fact]
    public async Task RerankAsync_EmptyDocuments_ReturnsEmptyWithoutCallingCohere()
    {
        var result = await Reranker().RerankAsync("q", [], 5, CancellationToken.None);

        result.Should().BeEmpty();
        A.CallTo(() => _api.RerankAsync(A<CohereRerankRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
