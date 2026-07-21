namespace MarqSpec.AgentForge.Api.Patient;

/// <summary>
/// The launched patient's identity plus a per-class reachability summary of their FHIR data - the
/// post-launch confirmation surface that stands in for the Epic 3 chat SPA (ARCHITECTURE.md §9).
/// Deliberately identity + counts, never a clinical data dump: USERS.md UC-1 (§6) is that the
/// product is an agent, not a static dashboard - the prioritized, cited brief is the agent's job.
/// </summary>
/// <param name="PatientId">The one patient this session is scoped to (from the launch, FR-CHAT-3).</param>
/// <param name="DisplayName">The patient's display name, or null if demographics were missing/unreachable.</param>
/// <param name="BirthDate">Date of birth, when present.</param>
/// <param name="Gender">FHIR administrative gender, when present.</param>
/// <param name="Demographics">Whether the Patient read succeeded (identity confirmation).</param>
/// <param name="Problems">Reachability + count of the problem list.</param>
/// <param name="Medications">Reachability + count of medication requests.</param>
/// <param name="Allergies">Reachability + count of allergies/intolerances.</param>
public sealed record PatientContextResult(
    string PatientId,
    string? DisplayName,
    DateOnly? BirthDate,
    string? Gender,
    ClinicalDataSummary Demographics,
    ClinicalDataSummary Problems,
    ClinicalDataSummary Medications,
    ClinicalDataSummary Allergies);

/// <summary>
/// One FHIR resource class's availability for the launched patient: whether the patient-scoped
/// fetch returned at all (<paramref name="Reachable"/>), and how many records it returned. A failed
/// fetch degrades to <c>Reachable: false, Count: 0</c> rather than blanking the whole page (UC-5).
/// </summary>
/// <param name="Reachable">True when the fetch completed; false when it failed and was isolated.</param>
/// <param name="Count">Records returned, or 0 when unreachable.</param>
public sealed record ClinicalDataSummary(bool Reachable, int Count);
