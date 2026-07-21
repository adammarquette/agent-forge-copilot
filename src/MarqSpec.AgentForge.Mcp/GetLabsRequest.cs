using System.ComponentModel.DataAnnotations;

namespace MarqSpec.AgentForge.Mcp;

/// <summary>
/// Input for the <c>get_labs</c> tool (ARCHITECTURE.md §8.1) - lab Observations (INR, K+, Cr,
/// lipids, BNP) with values, units, dates, and reference ranges.
/// </summary>
public sealed record GetLabsRequest
{
    /// <summary>OpenEMR multi-site segment.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Site { get; init; }

    /// <summary>The patient this query is scoped to (minimum-necessary - never whole-chart).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string PatientId { get; init; }

    /// <summary>
    /// A FHIR date-prefixed filter (e.g. <c>ge2026-01-01</c>), bounding the query to an interval
    /// rather than every lab ever recorded. Optional - omitted means unbounded by date.
    /// </summary>
    [RegularExpression(McpDateFilter.Pattern, ErrorMessage = McpDateFilter.ErrorMessage)]
    public string? SinceDate { get; init; }
}
