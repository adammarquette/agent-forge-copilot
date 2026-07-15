namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Reciprocal Rank Fusion of several ranked candidate lists into one ranking (W2_ARCHITECTURE.md §5): the
/// sparse (FTS) and dense (pgvector) halves of hybrid RAG are merged here. Each list contributes
/// <c>1/(k + rank)</c> to a candidate's fused score, so a candidate ranked well across lists outranks one
/// ranked well in only a single list, and <c>k</c> dampens the influence of deep ranks. Score-only fusion —
/// it never needs the raw relevance values, just the positions — which is what lets a keyword score and a
/// cosine distance be combined without normalizing incomparable scales.
/// </summary>
public static class ReciprocalRankFusion
{
    /// <summary>Default rank constant (Cormack, Clarke &amp; Buettcher 2009); dampens low-rank contributions.</summary>
    public const int DefaultK = 60;

    /// <summary>
    /// Fuses each already-best-first list in <paramref name="rankedLists"/> into one best-first ranking.
    /// Ties break by id (ordinal) so results are deterministic across runs.
    /// </summary>
    /// <param name="rankedLists">Candidate id lists, each ordered best-first.</param>
    /// <param name="k">Rank constant; larger flattens the score gap between ranks.</param>
    public static IReadOnlyList<FusedCandidate> Fuse(
        IEnumerable<IReadOnlyList<string>> rankedLists, int k = DefaultK)
    {
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var list in rankedLists)
        {
            // Only the best (first) occurrence of an id within a single list counts — a ranking shouldn't
            // repeat an id, and double-counting within one list would distort the fused score.
            var seenInList = new HashSet<string>(StringComparer.Ordinal);
            for (var rank = 0; rank < list.Count; rank++)
            {
                var id = list[rank];
                if (!seenInList.Add(id))
                {
                    continue;
                }

                scores[id] = scores.GetValueOrDefault(id) + 1.0 / (k + rank + 1);
            }
        }

        return
        [
            .. scores
                .Select(entry => new FusedCandidate(entry.Key, entry.Value))
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
        ];
    }
}

/// <summary>A candidate id and its fused relevance score (higher is better).</summary>
public sealed record FusedCandidate(string Id, double Score);
