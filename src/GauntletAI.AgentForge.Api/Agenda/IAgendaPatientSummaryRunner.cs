using GauntletAI.AgentForge.Agent;

namespace GauntletAI.AgentForge.Api.Agenda;

/// <summary>
/// Runs one patient's Daily Agenda summary turn (ARCHITECTURE.md §19) - extracted as its own seam
/// so <see cref="AgendaRosterService"/>'s fan-out (ordering, bounded concurrency, per-patient
/// failure isolation) can be unit-tested against a fake, independent of the real DI-scope-creation
/// mechanics this interface's implementation owns.
/// </summary>
public interface IAgendaPatientSummaryRunner
{
    /// <summary>
    /// Runs <paramref name="patientId"/>'s summary turn in its own DI scope - giving it a fresh
    /// correlation id (ARCHITECTURE.md §19.4) and its own <c>IScopedClinicianIdentityAccessor</c>
    /// instance, set to <paramref name="clinicianIdentity"/> before the orchestrator runs (FR-AUTH-4).
    /// </summary>
    Task<AgentTurnResult> RunAsync(string site, string patientId, string clinicianIdentity, CancellationToken cancellationToken);
}
