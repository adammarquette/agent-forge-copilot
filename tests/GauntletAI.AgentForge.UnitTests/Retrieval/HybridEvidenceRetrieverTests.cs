using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Observability;
using GauntletAI.AgentForge.Retrieval;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Retrieval;

/// <summary>
/// Unit tests for <see cref="HybridEvidenceRetriever"/> — the composition of sparse + dense retrieval, RRF
/// fusion, and rerank (W2_ARCHITECTURE.md §5). Guarded behavior: candidates from both halves are fused and
/// reranked; a failing dense half degrades to sparse-only; a failing reranker keeps the fused order; snippet
/// citation metadata survives the pipeline.
/// </summary>
public sealed class HybridEvidenceRetrieverTests
{
    private readonly ISparseRetriever _sparse = A.Fake<ISparseRetriever>();
    private readonly IDenseRetriever _dense = A.Fake<IDenseRetriever>();
    private readonly IReranker _reranker = A.Fake<IReranker>();
    private readonly IAgentForgeMetrics _metrics = A.Fake<IAgentForgeMetrics>();

    private HybridEvidenceRetriever CreateSut()
    {
        IReadOnlyList<EvidenceSnippet> none = [];
        A.CallTo(() => _sparse.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns(none);
        A.CallTo(() => _dense.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns(none);
        // Default reranker is identity: preserve input order, top-k, descending synthetic scores.
        A.CallTo(() => _reranker.RerankAsync(A<string>._, A<IReadOnlyList<RerankDocument>>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily((string _, IReadOnlyList<RerankDocument> docs, int k, CancellationToken _) =>
                (IReadOnlyList<RerankedCandidate>)[.. docs.Take(k).Select((d, i) => new RerankedCandidate(d.Id, 1.0 - (i * 0.01)))]);
        return new HybridEvidenceRetriever(_sparse, _dense, _reranker, _metrics, NullLogger<HybridEvidenceRetriever>.Instance);
    }

    private static EvidenceSnippet Snip(string chunkId, string text = "text") =>
        new() { DocumentId = "doc", Section = "sec", ChunkId = chunkId, Text = text, Score = 0 };

    [Fact]
    public async Task RetrieveAsync_WithBothHalves_FusesCandidatesThenAppliesRerankOrder()
    {
        var sut = CreateSut();
        A.CallTo(() => _sparse.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns((IReadOnlyList<EvidenceSnippet>)[Snip("a"), Snip("b")]);
        A.CallTo(() => _dense.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns((IReadOnlyList<EvidenceSnippet>)[Snip("c"), Snip("b")]);
        // Reranker picks c then a — an order neither half nor RRF would produce, proving rerank drives output.
        A.CallTo(() => _reranker.RerankAsync(A<string>._, A<IReadOnlyList<RerankDocument>>._, A<int>._, A<CancellationToken>._))
            .Returns((IReadOnlyList<RerankedCandidate>)[new("c", 0.9), new("a", 0.8)]);

        var result = await sut.RetrieveAsync("q", 2, CancellationToken.None);

        result.Select(r => r.ChunkId).Should().Equal("c", "a");
        // The reranker must have seen the full fused candidate pool (a, b, c).
        A.CallTo(() => _reranker.RerankAsync(A<string>._,
                A<IReadOnlyList<RerankDocument>>.That.Matches(d =>
                    d.Count == 3 && d.Any(x => x.Id == "a") && d.Any(x => x.Id == "b") && d.Any(x => x.Id == "c")),
                A<int>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _metrics.RecordRerankLatency(A<TimeSpan>._)).MustHaveHappened();
    }

    [Fact]
    public async Task RetrieveAsync_WhenDenseHalfThrows_DegradesToSparseOnly()
    {
        var sut = CreateSut();
        A.CallTo(() => _sparse.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns((IReadOnlyList<EvidenceSnippet>)[Snip("a"), Snip("b")]);
        A.CallTo(() => _dense.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Throws(new InvalidOperationException("embeddings provider down"));

        var result = await sut.RetrieveAsync("q", 5, CancellationToken.None);

        result.Select(r => r.ChunkId).Should().BeEquivalentTo(["a", "b"]);
        A.CallTo(() => _metrics.RecordRetrievalDegradation("dense")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RetrieveAsync_WhenRerankerThrows_KeepsFusedOrder()
    {
        var sut = CreateSut();
        A.CallTo(() => _sparse.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns((IReadOnlyList<EvidenceSnippet>)[Snip("a"), Snip("b")]);
        A.CallTo(() => _dense.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns((IReadOnlyList<EvidenceSnippet>)[Snip("b"), Snip("c")]);
        A.CallTo(() => _reranker.RerankAsync(A<string>._, A<IReadOnlyList<RerankDocument>>._, A<int>._, A<CancellationToken>._)).Throws(new InvalidOperationException("reranker down"));

        var result = await sut.RetrieveAsync("q", 3, CancellationToken.None);

        // "b" is ranked in both halves, so RRF puts it first; degradation must still return that fused order.
        result.Should().NotBeEmpty();
        result[0].ChunkId.Should().Be("b");
        A.CallTo(() => _metrics.RecordRetrievalDegradation("rerank")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RetrieveAsync_WhenBothHalvesEmpty_ReturnsEmptyWithoutReranking()
    {
        var result = await CreateSut().RetrieveAsync("q", 5, CancellationToken.None);

        result.Should().BeEmpty();
        A.CallTo(() => _reranker.RerankAsync(A<string>._, A<IReadOnlyList<RerankDocument>>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RetrieveAsync_PreservesCitationMetadata()
    {
        var sut = CreateSut();
        A.CallTo(() => _sparse.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns((IReadOnlyList<EvidenceSnippet>)[new() { DocumentId = "D1", Section = "S1", ChunkId = "x", Text = "hello", Score = 0 }]);

        var result = await sut.RetrieveAsync("q", 5, CancellationToken.None);

        var snippet = result.Single();
        snippet.DocumentId.Should().Be("D1");
        snippet.Section.Should().Be("S1");
        snippet.ChunkId.Should().Be("x");
        snippet.Text.Should().Be("hello");
    }
}
