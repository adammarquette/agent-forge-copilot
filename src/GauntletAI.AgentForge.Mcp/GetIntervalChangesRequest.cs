using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Mcp;

/// <summary>
/// Input for the <c>get_interval_changes</c> tool (ARCHITECTURE.md §8.1) - meds
/// started/changed, new labs, and interval encounters since the last visit. Unlike
/// <see cref="GetLabsRequest"/>/<see cref="GetVitalsRequest"/>, <see cref="SinceDate"/> is
/// required: an interval diff without a baseline date isn't the tool this is.
/// </summary>
public sealed record GetIntervalChangesRequest
{
    /// <summary>OpenEMR multi-site segment.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Site { get; init; }

    /// <summary>The patient this query is scoped to (minimum-necessary - never whole-chart).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string PatientId { get; init; }

    /// <summary>A FHIR date-prefixed filter (e.g. <c>ge2026-01-01</c>) - the last-visit baseline.</summary>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression(McpDateFilter.Pattern, ErrorMessage = McpDateFilter.ErrorMessage)]
    public required string SinceDate { get; init; }
}
