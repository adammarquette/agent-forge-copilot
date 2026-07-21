namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Patient demographics, mapped from a FHIR <c>Patient</c> (INTERFACE_CONTROL.md §B.1).
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="DisplayName">Given name(s) followed by family name, in FHIR array order.</param>
/// <param name="BirthDate">Date of birth, when present.</param>
/// <param name="Gender">FHIR administrative gender, when present.</param>
public sealed record PatientRecord(
    ClinicalSourceRef Source,
    string DisplayName,
    DateOnly? BirthDate,
    string? Gender);
