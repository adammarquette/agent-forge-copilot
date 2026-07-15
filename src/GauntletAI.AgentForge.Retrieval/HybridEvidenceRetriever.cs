using System.Diagnostics;
using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Observability;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Hybrid evidence retriever (W2_ARCHITECTURE.md §5, W2-D7): runs the sparse (Postgres FTS) and dense
/// (pgvector) halves, merges their candidate lists with Reciprocal Rank Fusion, then reranks the fused pool so
/// only the top grounded snippets reach the answer model. Degrades deterministically — a failing half or a
/// failing reranker is logged and the pipeline continues (sparse-only, or the fused order), never throwing
/// (§10). The two halves and the reranker are provider-agnostic seams; the Cohere/embedding wiring lives in
/// their implementations, not here.
/// </summary>
public sealed class HybridEvidenceRetriever : IEvidenceRetriever
{
    // Fan out wider than topK before rerank so the cross-encoder has real alternatives to reorder, not just
    // the final K. 4× is the "small corpus, basic but reliable" bar (§5); tune once baselines exist.
    private const int CandidatePoolMultiplier = 4;

    private readonly ISparseRetriever _sparse;
    private readonly IDenseRetriever _dense;
    private readonly IReranker _reranker;
    private readonly IAgentForgeMetrics _metrics;
    private readonly ILogger<HybridEvidenceRetriever> _logger;

    /// <summary>Creates the hybrid retriever over its two halves and the reranker.</summary>
    public HybridEvidenceRetriever(
        ISparseRetriever sparse, IDenseRetriever dense, IReranker reranker,
        IAgentForgeMetrics metrics, ILogger<HybridEvidenceRetriever> logger)
    {
        _sparse = sparse;
        _dense = dense;
        _reranker = reranker;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EvidenceSnippet>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken)
    {
        var poolSize = Math.Max(topK, topK * CandidatePoolMultiplier);

        var sparse = await SafeRetrieveAsync("sparse", ct => _sparse.RetrieveAsync(query, poolSize, ct), cancellationToken).ConfigureAwait(false);
        var dense = await SafeRetrieveAsync("dense", ct => _dense.RetrieveAsync(query, poolSize, ct), cancellationToken).ConfigureAwait(false);

        // Hydrate one snippet body per chunk id (first occurrence wins); RRF below decides the ranking, so it
        // doesn't matter which half a body came from.
        var byId = new Dictionary<string, EvidenceSnippet>(StringComparer.Ordinal);
        foreach (var snippet in sparse.Concat(dense))
        {
            byId.TryAdd(snippet.ChunkId, snippet);
        }

        if (byId.Count == 0)
        {
            return [];
        }

        var fused = ReciprocalRankFusion.Fuse(
        [
            [.. sparse.Select(s => s.ChunkId)],
            [.. dense.Select(s => s.ChunkId)],
        ]);

        var fusedSnippets = fused.Select(candidate => byId[candidate.Id] with { Score = candidate.Score }).ToList();

        return await RerankAsync(query, fusedSnippets, topK, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EvidenceSnippet>> RerankAsync(
        string query, IReadOnlyList<EvidenceSnippet> candidates, int topK, CancellationToken cancellationToken)
    {
        try
        {
            var documents = candidates.Select(s => new RerankDocument(s.ChunkId, s.Text)).ToList();
            var rerankStart = Stopwatch.GetTimestamp();
            var ranked = await _reranker.RerankAsync(query, documents, topK, cancellationToken).ConfigureAwait(false);
            _metrics.RecordRerankLatency(Stopwatch.GetElapsedTime(rerankStart));
            if (ranked.Count == 0)
            {
                return [.. candidates.Take(topK)];
            }

            var byId = candidates.ToDictionary(s => s.ChunkId, StringComparer.Ordinal);
            return
            [
                .. ranked
                    .Where(r => byId.ContainsKey(r.Id))
                    .Select(r => byId[r.Id] with { Score = r.Score })
                    .Take(topK)
            ];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RetrievalLog.RerankDegraded(_logger, ex);
            _metrics.RecordRetrievalDegradation("rerank");
            return [.. candidates.Take(topK)];
        }
    }

    private async Task<IReadOnlyList<EvidenceSnippet>> SafeRetrieveAsync(
        string half, Func<CancellationToken, Task<IReadOnlyList<EvidenceSnippet>>> retrieve, CancellationToken cancellationToken)
    {
        try
        {
            return await retrieve(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RetrievalLog.HalfDegraded(_logger, half, ex);
            _metrics.RecordRetrievalDegradation(half);
            return [];
        }
    }
}
