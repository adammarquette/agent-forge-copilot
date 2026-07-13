using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        HttpContext httpContext, SmartLaunchService launchService, ILoggerFactory loggerFactory,
        string? iss, string? launch)
    {
        _ = iss; // Present per SMART launch (INTERFACE_CONTROL.md A.3); not needed beyond the configured connection (v1: single fixed OpenEMR deployment).
        var (authorizeUrl, pending) = launchService.BeginLaunch(launch);

        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        httpContext.Session.SetString(PendingLaunchStateKey, pending.State);
        httpContext.Session.SetString(PendingLaunchVerifierKey, pending.CodeVerifier);
        await httpContext.Session.CommitAsync(httpContext.RequestAborted).ConfigureAwait(false);

        // reference: gitlab#67 - temporary diagnostic; compare host + sessionId against the callback's.
#pragma warning disable CA1873 // Information is enabled in deployed envs; this is short-lived diagnostic code.
        LaunchEndpointsLog.LaunchDiag(
            loggerFactory.CreateLogger("LaunchEndpoints"), httpContext.Request.Host.ToString(), httpContext.Session.Id);
#pragma warning restore CA1873

        return Results.Redirect(authorizeUrl.ToString());
    }

    private static async Task<IResult> HandleCallbackAsync(
        HttpContext httpContext, SmartLaunchService launchService, IOptions<BffOptions> bffOptions,
        ILoggerFactory loggerFactory, string code, string state)
    {
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var pendingState = httpContext.Session.GetString(PendingLaunchStateKey);
        var pendingVerifier = httpContext.Session.GetString(PendingLaunchVerifierKey);

        // reference: gitlab#67 - temporary diagnostic. host + sessionId vs the launch's, and whether
        // the session cookie even arrived, pinpoint why the pending launch isn't found.
#pragma warning disable CA1873 // Information is enabled in deployed envs; this is short-lived diagnostic code.
        LaunchEndpointsLog.CallbackDiag(
            loggerFactory.CreateLogger("LaunchEndpoints"), httpContext.Request.Host.ToString(), httpContext.Session.Id,
            httpContext.Request.Cookies.ContainsKey(".AspNetCore.Session"),
            string.Join(",", httpContext.Request.Cookies.Keys), !string.IsNullOrEmpty(pendingState));
#pragma warning restore CA1873

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
