using System.ComponentModel.DataAnnotations;

namespace MarqSpec.AgentForge.Mcp;

/// <summary>
/// Input for the <c>get_recent_encounters</c> tool (ARCHITECTURE.md §8.1) - a thin list (date,
/// type, reason); the agent requests detail explicitly rather than pulling everything upfront.
/// </summary>
public sealed record GetRecentEncountersRequest
{
    /// <summary>OpenEMR multi-site segment.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Site { get; init; }

    /// <summary>The patient this query is scoped to (minimum-necessary - never whole-chart).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string PatientId { get; init; }

    /// <summary>How many of the most recent encounters to return. Bounded to prevent an unbounded pull.</summary>
    [Range(1, 20)]
    public int Count { get; init; } = 3;
}
