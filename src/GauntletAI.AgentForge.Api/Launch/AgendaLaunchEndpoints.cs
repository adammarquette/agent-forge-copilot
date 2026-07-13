using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// Thin HTTP layer around <see cref="AgendaLaunchService"/> - the mirror image of
/// <see cref="LaunchEndpoints"/>: reads/writes the browser session and issues redirects, leaving
/// the actual launch/callback logic in the already-tested service (ARCHITECTURE.md §19).
/// </summary>
public static class AgendaLaunchEndpoints
{
    private const string PendingAgendaLaunchStateKey = "pending-agenda-launch.state";
    private const string PendingAgendaLaunchVerifierKey = "pending-agenda-launch.code-verifier";

    /// <summary>Maps the Daily Agenda SMART launch and callback endpoints.</summary>
    public static IEndpointRouteBuilder MapAgendaLaunchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/agenda/launch", HandleLaunchAsync);
        endpoints.MapGet("/agenda/callback", HandleCallbackAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleLaunchAsync(
        HttpContext httpContext, AgendaLaunchService launchService, string? iss, string? launch)
    {
        _ = iss; // Present per SMART launch (INTERFACE_CONTROL.md A.3); not needed beyond the configured connection (v1: single fixed OpenEMR deployment).
        var (authorizeUrl, pending) = launchService.BeginLaunch(launch);

        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        httpContext.Session.SetString(PendingAgendaLaunchStateKey, pending.State);
        httpContext.Session.SetString(PendingAgendaLaunchVerifierKey, pending.CodeVerifier);
        await httpContext.Session.CommitAsync(httpContext.RequestAborted).ConfigureAwait(false);

        return Results.Redirect(authorizeUrl.ToString());
    }

    private static async Task<IResult> HandleCallbackAsync(
        HttpContext httpContext, AgendaLaunchService launchService, IOptions<BffOptions> bffOptions,
        string code, string state)
    {
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var pendingState = httpContext.Session.GetString(PendingAgendaLaunchStateKey);
        var pendingVerifier = httpContext.Session.GetString(PendingAgendaLaunchVerifierKey);

        if (string.IsNullOrEmpty(pendingState) || string.IsNullOrEmpty(pendingVerifier))
        {
            return Results.BadRequest("No pending agenda launch for this session.");
        }

        var pending = new PendingLaunchContext(pendingState, pendingVerifier);

        AgendaSessionContext session;
        try
        {
            session = await launchService.CompleteLaunchAsync(code, state, pending, httpContext.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (AgendaLaunchException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        httpContext.Session.Remove(PendingAgendaLaunchStateKey);
        httpContext.Session.Remove(PendingAgendaLaunchVerifierKey);
        httpContext.Session.SaveAgendaSession(session);
        await httpContext.Session.CommitAsync(httpContext.RequestAborted).ConfigureAwait(false);

        // Prefix the reverse-proxy PathBase (/agentforge) - a bare "/agenda" redirect lands at the
        // proxy root, which isn't routed to this service, so it 404s (reference: gitlab#67).
        return Results.Redirect(httpContext.Request.PathBase.Add(bffOptions.Value.AgendaPath).ToString());
    }
}
