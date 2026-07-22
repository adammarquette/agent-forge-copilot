using MarqSpec.AgentForge.Agents;

namespace MarqSpec.AgentForge.Retrieval;

/// <summary>
/// The sparse (keyword / Postgres FTS) half of hybrid retrieval (W2_ARCHITECTURE.md §5). Kept as its own seam
/// — separate from the public <see cref="IEvidenceRetriever"/> — so <see cref="HybridEvidenceRetriever"/> can
/// compose it with the dense half and fuse the two, and so each half is independently faked in tests.
/// </summary>
public interface ISparseRetriever
{
    /// <summary>Returns up to <paramref name="topK"/> keyword-matched candidate snippets, best-first.</summary>
    Task<IReadOnlyList<EvidenceSnippet>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken);
}
