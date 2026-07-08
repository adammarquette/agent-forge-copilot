namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// A medication dispense event, mapped from a FHIR <c>MedicationDispense</c>
/// (INTERFACE_CONTROL.md §B.1). Fill dates and days-supply are the adherence signal called out
/// in USERS.md §3 ("what was started/stopped/titrated, and any adherence signal").
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="MedicationDisplay">Human-readable medication name/strength.</param>
/// <param name="Status">FHIR <c>MedicationDispense.status</c> (completed, in-progress, ...).</param>
/// <param name="WhenHandedOver">When the fill was picked up, when present.</param>
/// <param name="DaysSupply">Days-supply of the fill, when present - the adherence-gap signal.</param>
public sealed record MedicationDispenseRecord(
    ClinicalSourceRef Source,
    string MedicationDisplay,
    string Status,
    DateTimeOffset? WhenHandedOver,
    double? DaysSupply);
