using FluentAssertions;
using GauntletAI.AgentForge.Retrieval;

namespace GauntletAI.AgentForge.UnitTests.Retrieval;

/// <summary>
/// Unit tests for <see cref="ReciprocalRankFusion"/> — the sparse+dense candidate merge (W2_ARCHITECTURE.md
/// §5). Guarded behavior: a candidate ranked well in both lists must outrank one ranked well in only one;
/// candidates unique to a single list are still included; output is ordered best-first and deterministic.
/// </summary>
public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void Fuse_CandidateInBothLists_OutranksCandidateTopOfOnlyOne()
    {
        // "b" is #2 in both lists; "a" is #1 in only the sparse list. Agreement across lists should let
        // "b" overtake a single-list leader — the whole point of fusion.
        IReadOnlyList<string> sparse = ["a", "b", "c"];
        IReadOnlyList<string> dense = ["d", "b", "e"];

        var fused = ReciprocalRankFusion.Fuse([sparse, dense]);

        fused.Select(f => f.Id).Should().StartWith("b");
    }

    [Fact]
    public void Fuse_IncludesCandidatesUniqueToASingleList()
    {
        IReadOnlyList<string> sparse = ["a"];
        IReadOnlyList<string> dense = ["z"];

        var fused = ReciprocalRankFusion.Fuse([sparse, dense]);

        fused.Select(f => f.Id).Should().BeEquivalentTo(["a", "z"]);
    }

    [Fact]
    public void Fuse_OrdersByDescendingFusedScore()
    {
        IReadOnlyList<string> sparse = ["a", "b", "c"];
        IReadOnlyList<string> dense = ["a", "b", "c"];

        var fused = ReciprocalRankFusion.Fuse([sparse, dense]);

        fused.Select(f => f.Id).Should().ContainInOrder("a", "b", "c");
        fused.Select(f => f.Score).Should().BeInDescendingOrder();
    }

    [Fact]
    public void Fuse_HigherRankContributesMoreThanLowerRank()
    {
        // A single list: rank-1 must score strictly higher than rank-2 (1/(k+1) > 1/(k+2)).
        IReadOnlyList<string> only = ["top", "bottom"];

        var fused = ReciprocalRankFusion.Fuse([only]);

        fused.First(f => f.Id == "top").Score.Should().BeGreaterThan(fused.First(f => f.Id == "bottom").Score);
    }

    [Fact]
    public void Fuse_LargerK_FlattensTheScoreGapBetweenRanks()
    {
        IReadOnlyList<string> only = ["top", "bottom"];

        var tight = ReciprocalRankFusion.Fuse([only], k: 10);
        var flat = ReciprocalRankFusion.Fuse([only], k: 1000);

        double gap(IReadOnlyList<FusedCandidate> r) =>
            r.First(f => f.Id == "top").Score - r.First(f => f.Id == "bottom").Score;
        gap(flat).Should().BeLessThan(gap(tight));
    }

    [Fact]
    public void Fuse_EmptyLists_ReturnsEmpty()
    {
        ReciprocalRankFusion.Fuse([]).Should().BeEmpty();
        ReciprocalRankFusion.Fuse([Array.Empty<string>(), Array.Empty<string>()]).Should().BeEmpty();
    }

    [Fact]
    public void Fuse_EqualScores_BreaksTiesDeterministically()
    {
        // Two candidates each appear once at rank 1 in different lists -> identical fused score. Order must be
        // stable (by id) so retrieval results don't reshuffle run to run.
        IReadOnlyList<string> sparse = ["y"];
        IReadOnlyList<string> dense = ["x"];

        var first = ReciprocalRankFusion.Fuse([sparse, dense]);
        var second = ReciprocalRankFusion.Fuse([sparse, dense]);

        first.Select(f => f.Id).Should().Equal(second.Select(f => f.Id));
        first.Select(f => f.Id).Should().Equal("x", "y");
    }
}
