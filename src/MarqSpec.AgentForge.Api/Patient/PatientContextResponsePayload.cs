namespace MarqSpec.AgentForge.Api.Patient;

/// <summary>Wire shape for <c>GET /patient</c> - the launched patient's identity + FHIR reachability.</summary>
/// <param name="PatientId">The one patient this session is scoped to.</param>
/// <param name="DisplayName">Display name, or null when demographics were missing/unreachable.</param>
/// <param name="BirthDate">Date of birth, when present.</param>
/// <param name="Gender">FHIR administrative gender, when present.</param>
/// <param name="Demographics">Whether the Patient read succeeded.</param>
/// <param name="Problems">Reachability + count of the problem list.</param>
/// <param name="Medications">Reachability + count of medication requests.</param>
/// <param name="Allergies">Reachability + count of allergies/intolerances.</param>
public sealed record PatientContextResponsePayload(
    string PatientId,
    string? DisplayName,
    DateOnly? BirthDate,
    string? Gender,
    ClinicalDataSummary Demographics,
    ClinicalDataSummary Problems,
    ClinicalDataSummary Medications,
    ClinicalDataSummary Allergies);
