using System.Globalization;

namespace MarqSpec.AgentForge.Mcp;

/// <summary>
/// Shared validation and parsing for the FHIR date-prefixed filter strings (e.g.
/// <c>ge2026-01-01</c>) several tool requests accept, per the search-param shape confirmed in
/// INTERFACE_CONTROL.md §B.2.
/// </summary>
public static class McpDateFilter
{
    /// <summary>Requires one of the FHIR search-prefix codes followed by a date, e.g. <c>ge2026-01-01</c>.</summary>
    public const string Pattern = @"^(eq|ne|gt|lt|ge|le|sa|eb|ap)\d{4}(-\d{2}(-\d{2})?)?$";

    /// <summary>Validation failure message for <see cref="Pattern"/>.</summary>
    public const string ErrorMessage =
        "must be a FHIR date-prefixed filter, e.g. 'ge2026-01-01' (prefix required: eq/ne/gt/lt/ge/le/sa/eb/ap).";

    private const int PrefixLength = 2;

    /// <summary>
    /// Extracts the date portion of a filter already validated against <see cref="Pattern"/>
    /// (e.g. <c>ge2026-01-01</c> -&gt; 2026-01-01T00:00:00Z), for client-side comparisons where
    /// the FHIR server can't filter server-side (e.g. MedicationRequest has no date search param).
    /// </summary>
    public static DateTimeOffset ExtractDate(string fhirDateFilter) =>
        DateTimeOffset.Parse(
            fhirDateFilter[PrefixLength..],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
