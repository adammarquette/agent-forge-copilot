namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// Shared validation for the FHIR date-prefixed filter strings (e.g. <c>ge2026-01-01</c>) several
/// tool requests accept, per the search-param shape confirmed in INTERFACE_CONTROL.md §B.2.
/// </summary>
internal static class McpDateFilter
{
    /// <summary>Requires one of the FHIR search-prefix codes followed by a date, e.g. <c>ge2026-01-01</c>.</summary>
    public const string Pattern = @"^(eq|ne|gt|lt|ge|le|sa|eb|ap)\d{4}(-\d{2}(-\d{2})?)?$";

    /// <summary>Validation failure message for <see cref="Pattern"/>.</summary>
    public const string ErrorMessage =
        "must be a FHIR date-prefixed filter, e.g. 'ge2026-01-01' (prefix required: eq/ne/gt/lt/ge/le/sa/eb/ap).";
}
