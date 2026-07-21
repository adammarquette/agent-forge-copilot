using MarqSpec.AgentForge.Api.Session;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.Api.Patient;

/// <summary>
/// Builds the post-launch patient-context confirmation for the one patient a session is scoped to:
/// demographics for identity, and a per-class reachability summary proving the patient-scoped FHIR
/// path works end-to-end. Thin and read-only by design - the cited clinical brief is the Epic 3
/// agent, not this (USERS.md UC-1). Mirrors <c>AgendaRosterService</c>: sets the AsyncLocal access
/// token once, then isolates each fetch so one failing resource degrades to "unreachable" instead
/// of blanking the page (UC-5).
/// </summary>
public sealed class PatientContextService(
    IOpenEmrFhirClient fhirClient,
    IScopedAccessTokenProvider tokenProvider,
    ILogger<PatientContextService> logger)
{
    /// <summary>Builds <paramref name="session"/>'s launched-patient context confirmation.</summary>
    public async Task<PatientContextResult> BuildAsync(PatientSessionContext session, CancellationToken cancellationToken)
    {
        // AsyncLocal-backed (ScopedAccessTokenProvider); the FHIR client's AuthHandler reads it back.
        tokenProvider.AccessToken = session.AccessToken;

        PatientRecord? patient = null;
        var demographicsReachable = true;
        try
        {
            patient = await fhirClient.GetPatientAsync(session.Site, session.PatientId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PatientContextServiceLog.FetchFailed(logger, "Patient", session.PatientId, ex.Message);
            demographicsReachable = false;
        }

        var problems = await SummarizeAsync(
            "Condition", session.PatientId,
            () => fhirClient.GetConditionsAsync(session.Site, session.PatientId, cancellationToken)).ConfigureAwait(false);
        var medications = await SummarizeAsync(
            "MedicationRequest", session.PatientId,
            () => fhirClient.GetMedicationRequestsAsync(session.Site, session.PatientId, cancellationToken)).ConfigureAwait(false);
        var allergies = await SummarizeAsync(
            "AllergyIntolerance", session.PatientId,
            () => fhirClient.GetAllergiesAsync(session.Site, session.PatientId, cancellationToken)).ConfigureAwait(false);

        return new PatientContextResult(
            session.PatientId,
            patient?.DisplayName,
            patient?.BirthDate,
            patient?.Gender,
            new ClinicalDataSummary(demographicsReachable, patient is null ? 0 : 1),
            problems,
            medications,
            allergies);
    }

    private async Task<ClinicalDataSummary> SummarizeAsync<T>(
        string resourceType, string patientId, Func<Task<IReadOnlyList<T>>> fetch)
    {
        try
        {
            var items = await fetch().ConfigureAwait(false);
            return new ClinicalDataSummary(Reachable: true, Count: items.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PatientContextServiceLog.FetchFailed(logger, resourceType, patientId, ex.Message);
            return new ClinicalDataSummary(Reachable: false, Count: 0);
        }
    }
}
