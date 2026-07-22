using MarqSpec.AgentForge.Agent;
using MarqSpec.AgentForge.Api.Session;
using Microsoft.Extensions.DependencyInjection;

namespace MarqSpec.AgentForge.Api.Agenda;

/// <summary>
/// Real implementation of <see cref="IAgendaPatientSummaryRunner"/>: new scope, set the
/// clinician identity in that scope, resolve the orchestrator, run the turn.
/// </summary>
/// <remarks>
/// The access token does not need to be set here: <see cref="ScopedAccessTokenProvider"/> is
/// backed by a <c>static</c> <see cref="AsyncLocal{T}"/>, which flows through a new DI scope on
/// its own (it isn't itself a scoped service) - <see cref="AgendaRosterService"/> sets it once
/// before the whole fan-out starts. <see cref="ScopedClinicianIdentityAccessor"/> has no such
/// ambient flow (a plain property, registered Scoped), so it must be set fresh inside every new
/// scope this runner creates, or FR-AUTH-4 audit attribution would silently break for every
/// summary after the first.
/// </remarks>
public sealed class AgendaPatientSummaryRunner(IServiceScopeFactory scopeFactory) : IAgendaPatientSummaryRunner
{
    /// <inheritdoc />
    public async Task<AgentTurnResult> RunAsync(
        string site, string patientId, string clinicianIdentity, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IScopedClinicianIdentityAccessor>().ClinicianIdentity = clinicianIdentity;

        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
        return await orchestrator.StartAgendaSummaryAsync(site, patientId, cancellationToken).ConfigureAwait(false);
    }
}
