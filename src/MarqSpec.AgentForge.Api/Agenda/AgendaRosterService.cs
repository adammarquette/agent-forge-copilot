using MarqSpec.AgentForge.Agent;
using MarqSpec.AgentForge.Api.Chat;
using MarqSpec.AgentForge.Api.Session;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.Api.Agenda;

/// <summary>
/// Builds the Daily Agenda for one clinician (ARCHITECTURE.md §19): fetches today's roster,
/// filters it to this provider's not-yet-occurred appointments, and fans out a short per-patient
/// summary with bounded concurrency and per-patient failure isolation. Calls
/// <see cref="IAgentOrchestrator"/> only through <see cref="IAgendaPatientSummaryRunner"/> - never
/// through <see cref="ChatSessionCoordinator"/>, which is hard-wired to one chat session and
/// doesn't fit an N-patient fan-out.
/// </summary>
public sealed class AgendaRosterService(
    IOpenEmrFhirClient fhirClient,
    IAgendaPatientSummaryRunner summaryRunner,
    IScopedAccessTokenProvider tokenProvider,
    TimeProvider timeProvider,
    IOptions<AgendaOptions> agendaOptions,
    ILogger<AgendaRosterService> logger)
{
    private static readonly string[] ExcludedStatuses = ["cancelled", "noshow", "entered-in-error"];

    /// <summary>Builds <paramref name="session"/>'s clinician's Daily Agenda.</summary>
    public async Task<AgendaResult> BuildAgendaAsync(AgendaSessionContext session, CancellationToken cancellationToken)
    {
        // The access token is AsyncLocal-backed (ScopedAccessTokenProvider) and flows into every
        // fan-out branch's new DI scope on its own - set once here, not per-patient, since it's
        // the same provider/token throughout (unlike clinician identity, which
        // IAgendaPatientSummaryRunner sets fresh per patient - see its own remarks).
        tokenProvider.AccessToken = session.AccessToken;

        var now = timeProvider.GetUtcNow();
        var dateFilter = $"eq{now:yyyy-MM-dd}";
        var appointments = await fhirClient.GetAppointmentsAsync(session.Site, dateFilter, cancellationToken).ConfigureAwait(false);

        var roster = appointments
            .Where(a => !string.IsNullOrEmpty(a.PatientId))
            .Where(a => a.IsForProvider(session.ClinicianIdentity))
            .Where(a => !ExcludedStatuses.Contains(a.Status, StringComparer.OrdinalIgnoreCase))
            .Where(a => a.ScheduledStart is { } start && start > now)
            .OrderBy(a => a.ScheduledStart)
            .ToList();

        var rows = new AgendaRow?[roster.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, roster.Count),
            new ParallelOptions { MaxDegreeOfParallelism = agendaOptions.Value.MaxConcurrentSummaries, CancellationToken = cancellationToken },
            async (index, ct) =>
            {
                var appointment = roster[index];
                var patientId = appointment.PatientId!;
                var displayName = await ResolveDisplayNameOrNullAsync(session.Site, patientId, ct).ConfigureAwait(false);
                try
                {
                    var result = await summaryRunner.RunAsync(session.Site, patientId, session.ClinicianIdentity, ct).ConfigureAwait(false);
                    rows[index] = new AgendaRow(patientId, displayName, appointment.ScheduledStart!.Value, result.Answer, result.SafetyFlags, Failed: false, FailureReason: null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AgendaRosterServiceLog.PatientSummaryFailed(logger, patientId, ex.Message);
                    rows[index] = new AgendaRow(
                        patientId, displayName, appointment.ScheduledStart!.Value, Summary: null, SafetyFlags: [],
                        Failed: true, FailureReason: "Summary unavailable for this patient right now.");
                }
            }).ConfigureAwait(false);

        return new AgendaResult([.. rows!], now);
    }

    // Best-effort: a failed demographics read must not fail the row (UC-5) - the UI falls back to
    // "Patient {id}". Isolated from the summary so a name lookup can't take a good summary down.
    private async Task<string?> ResolveDisplayNameOrNullAsync(string site, string patientId, CancellationToken cancellationToken)
    {
        try
        {
            var patient = await fhirClient.GetPatientAsync(site, patientId, cancellationToken).ConfigureAwait(false);
            return patient?.DisplayName;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AgendaRosterServiceLog.PatientNameUnavailable(logger, patientId, ex.Message);
            return null;
        }
    }
}
