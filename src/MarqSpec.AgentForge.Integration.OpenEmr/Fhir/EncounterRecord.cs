namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// An interval event, mapped from a FHIR <c>Encounter</c> (INTERFACE_CONTROL.md §B.1). Drives
/// the "since last visit" diff at the heart of UC-1.
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="EncounterType">Human-readable encounter type, e.g. "Office Visit", "ED Visit".</param>
/// <param name="Status">FHIR <c>Encounter.status</c> (finished, in-progress, ...).</param>
/// <param name="PeriodStart">When the encounter began, when present.</param>
/// <param name="PeriodEnd">When the encounter ended, when present (omitted while in progress).</param>
/// <param name="ReasonDisplay">Reason for the encounter, when present.</param>
public sealed record EncounterRecord(
    ClinicalSourceRef Source,
    string EncounterType,
    string Status,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    string? ReasonDisplay);
