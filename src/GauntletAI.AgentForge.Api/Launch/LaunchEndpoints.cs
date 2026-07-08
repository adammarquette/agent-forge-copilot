using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// Thin HTTP layer around <see cref="SmartLaunchService"/>: reads/writes the browser session and
/// issues redirects, leaving the actual launch/callback logic in the already-tested service.
/// </summary>
public static class LaunchEndpoints
{
    private const string PendingLaunchStateKey = "pending-launch.state";
    private const string PendingLaunchVerifierKey = "pending-launch.code-verifier";

    /// <summary>Maps the SMART EHR launch and callback endpoints.</summary>
    public static IEndpointRouteBuilder MapLaunchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/launch", HandleLaunchAsync);
        endpoints.MapGet("/callback", HandleCallbackAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleLaunchAsync(
        HttpContext httpContext, SmartLaunchService launchService, string? iss, string? launch)
    {
        _ = iss; // Present per SMART launch (INTERFACE_CONTROL.md A.3); not needed beyond the configured connection (v1: single fixed OpenEMR deployment).
        var (authorizeUrl, pending) = launchService.BeginLaunch(launch);

        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        httpContext.Session.SetString(PendingLaunchStateKey, pending.State);
        httpContext.Session.SetString(PendingLaunchVerifierKey, pending.CodeVerifier);
        await httpContext.Session.CommitAsync(httpContext.RequestAborted).ConfigureAwait(false);

        return Results.Redirect(authorizeUrl.ToString());
    }

    private static async Task<IResult> HandleCallbackAsync(
        HttpContext httpContext, SmartLaunchService launchService, IOptions<BffOptions> bffOptions,
        string code, string state)
    {
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var pendingState = httpContext.Session.GetString(PendingLaunchStateKey);
        var pendingVerifier = httpContext.Session.GetString(PendingLaunchVerifierKey);

        if (string.IsNullOrEmpty(pendingState) || string.IsNullOrEmpty(pendingVerifier))
        {
            return Results.BadRequest("No pending SMART launch for this session.");
        }

        var pending = new PendingLaunchContext(pendingState, pendingVerifier);

        PatientSessionContext session;
        try
        {
            session = await launchService.CompleteLaunchAsync(code, state, pending, httpContext.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (SmartLaunchException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        httpContext.Session.Remove(PendingLaunchStateKey);
        httpContext.Session.Remove(PendingLaunchVerifierKey);
        httpContext.Session.SavePatientSession(session);
        await httpContext.Session.CommitAsync(httpContext.RequestAborted).ConfigureAwait(false);

        return Results.Redirect(bffOptions.Value.ChatPath);
    }
}
