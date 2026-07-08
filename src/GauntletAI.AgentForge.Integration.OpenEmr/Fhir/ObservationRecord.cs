namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// A lab or vital-sign result, mapped from a FHIR <c>Observation</c>
/// (INTERFACE_CONTROL.md §B.1). Carries the reference range alongside the value so the
/// verification layer's domain-constraint rules (INR range, K+/creatinine thresholds, ...) have
/// what they need without a second lookup.
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="Category">FHIR <c>Observation.category</c> code: <c>laboratory</c> or <c>vital-signs</c>.</param>
/// <param name="CodeDisplay">Human-readable observation name, e.g. "INR", "Heart rate".</param>
/// <param name="Value">Numeric result, when the observation is quantitative.</param>
/// <param name="Unit">Unit for <see cref="Value"/>, when present.</param>
/// <param name="ReferenceRangeLow">Lower bound of the normal range, when present.</param>
/// <param name="ReferenceRangeHigh">Upper bound of the normal range, when present.</param>
/// <param name="EffectiveDateTime">When the observation was made, when present.</param>
/// <param name="Status">FHIR <c>Observation.status</c> (final, preliminary, ...).</param>
public sealed record ObservationRecord(
    ClinicalSourceRef Source,
    string Category,
    string CodeDisplay,
    double? Value,
    string? Unit,
    double? ReferenceRangeLow,
    double? ReferenceRangeHigh,
    DateTimeOffset? EffectiveDateTime,
    string Status);
