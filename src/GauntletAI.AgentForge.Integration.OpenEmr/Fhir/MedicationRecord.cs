namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// A cardiology medication, mapped from a FHIR <c>MedicationRequest</c>
/// (INTERFACE_CONTROL.md §B.1).
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="MedicationDisplay">Human-readable medication name/strength.</param>
/// <param name="Dosage">Free-text dosage instruction, when present.</param>
/// <param name="Status">FHIR <c>MedicationRequest.status</c> (active, stopped, completed, ...).</param>
/// <param name="AuthoredOn">When the request was authored, when present.</param>
public sealed record MedicationRecord(
    ClinicalSourceRef Source,
    string MedicationDisplay,
    string? Dosage,
    string Status,
    DateTimeOffset? AuthoredOn);
