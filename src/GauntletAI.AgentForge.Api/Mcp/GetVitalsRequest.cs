using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>Input for the <c>get_vitals</c> tool (ARCHITECTURE.md §8.1) - vital-signs Observations (BP, HR).</summary>
public sealed record GetVitalsRequest
{
    /// <summary>OpenEMR multi-site segment.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Site { get; init; }

    /// <summary>The patient this query is scoped to (minimum-necessary - never whole-chart).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string PatientId { get; init; }

    /// <summary>A FHIR date-prefixed filter (e.g. <c>ge2026-01-01</c>). Optional.</summary>
    [RegularExpression(McpDateFilter.Pattern, ErrorMessage = McpDateFilter.ErrorMessage)]
    public string? SinceDate { get; init; }
}
