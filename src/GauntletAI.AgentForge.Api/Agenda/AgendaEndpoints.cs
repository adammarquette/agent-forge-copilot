using GauntletAI.AgentForge.Api.Chat;
using GauntletAI.AgentForge.Api.Launch;
using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Api.Agenda;

/// <summary>
/// The Daily Agenda's own HTTP surface (ARCHITECTURE.md §19): the roster fetch, and the
/// drill-down that hands a selected patient off to the existing, completely unchanged
/// single-patient chat. Thin by design - the same shape as <see cref="LaunchEndpoints"/> and
/// <see cref="AgendaLaunchEndpoints"/>, leaving the real logic in already-tested services.
/// </summary>
public static class AgendaEndpoints
{
    /// <summary>Maps the Daily Agenda's roster and drill-down endpoints.</summary>
    public static IEndpointRouteBuilder MapAgendaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/agenda", HandleGetAgendaAsync);
        endpoints.MapPost("/agenda/select-patient", HandleSelectPatientAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleGetAgendaAsync(
        HttpContext httpContext, AgendaRosterService rosterService)
    {
        var session = await GetAuthenticatedAgendaSessionOrNullAsync(httpContext).ConfigureAwait(false);
        if (session is null)
        {
            return Results.Unauthorized();
        }

        var agenda = await rosterService.BuildAgendaAsync(session, httpContext.RequestAborted).ConfigureAwait(false);

        httpContext.Session.SaveAgendaRoster(agenda.Rows.Select(r => r.PatientId));
        await httpContext.Session.CommitAsync(httpContext.RequestAborted).ConfigureAwait(false);

        return Results.Ok(ToPayload(agenda));
    }

    private static async Task<IResult> HandleSelectPatientAsync(
        HttpContext httpContext, IOptions<BffOptions> bffOptions, ILoggerFactory loggerFactory, string patientId)
    {
        var session = await GetAuthenticatedAgendaSessionOrNullAsync(httpContext).ConfigureAwait(false);
        if (session is null)
        {
            return Results.Unauthorized();
        }

        var roster = httpContext.Session.TryGetAgendaRoster();
        if (!AgendaRosterGate.Authorize(roster, patientId))
        {
            AgendaEndpointsLog.PatientNotInRoster(loggerFactory.CreateLogger(nameof(AgendaEndpoints)), session.ClinicianIdentity, patientId);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var patientSession = new PatientSessionContext(session.AccessToken, session.Site, patientId, session.ClinicianIdentity);
        httpContext.Session.SavePatientSession(patientSession);
        await httpContext.Session.CommitAsync(httpContext.RequestAborted).ConfigureAwait(false);

        // Prefix the reverse-proxy PathBase (/agentforge), same as the launch endpoints - a bare
        // "/index.html" redirect lands at the proxy root and 404s (the drill-down's 404-in-tab bug).
        return Results.Redirect(httpContext.Request.PathBase.Add(bffOptions.Value.ChatPath).ToString());
    }

    private static async Task<AgendaSessionContext?> GetAuthenticatedAgendaSessionOrNullAsync(HttpContext httpContext)
    {
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        return httpContext.Session.TryGetAgendaSession();
    }

    private static AgendaResponsePayload ToPayload(AgendaResult result) => new(
        [.. result.Rows.Select(r => new AgendaRowPayload(
            r.PatientId,
            r.ScheduledStart,
            r.Summary,
            [.. r.SafetyFlags.Select(f => new SafetyFlagPayload(f.RuleId, f.Description, [.. f.Sources.Select(s => s.Citation)]))],
            r.Failed,
            r.FailureReason))],
        result.AsOf);
}
