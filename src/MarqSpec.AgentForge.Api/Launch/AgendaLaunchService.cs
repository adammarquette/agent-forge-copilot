using MarqSpec.AgentForge.Api.Session;
using MarqSpec.AgentForge.Integration.OpenEmr;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.Api.Launch;

/// <summary>
/// Drives the roster-level Daily Agenda SMART launch (ARCHITECTURE.md §19) - the mirror image of
/// <see cref="SmartLaunchService"/>: same PKCE/state/introspection mechanics, but authenticates as
/// its own registered OAuth client (<see cref="AgendaOpenEmrOptions"/>) and succeeds with NO launch
/// patient context, producing an <see cref="AgendaSessionContext"/> instead of a
/// <see cref="PatientSessionContext"/>. Deliberately a standalone copy rather than a branch inside
/// <see cref="SmartLaunchService"/>, so the existing single-patient launch's behavior and tests stay
/// completely untouched.
/// </summary>
public sealed class AgendaLaunchService(
    IOpenEmrAuthClient authClient,
    IOptions<OpenEmrOptions> openEmrOptions,
    IOptions<AgendaOpenEmrOptions> agendaOptions,
    IOptions<BffOptions> bffOptions,
    ILogger<AgendaLaunchService> logger)
{
    /// <summary>
    /// Builds the authorize redirect for a roster-level launch. Returns the URL to redirect the
    /// browser to, and the pending context the caller must persist (session) until the callback
    /// arrives.
    /// </summary>
    public (Uri AuthorizeUrl, PendingLaunchContext Pending) BeginLaunch(string? launchToken)
    {
        var openEmr = openEmrOptions.Value;
        var agenda = agendaOptions.Value;
        var pkce = PkceGenerator.Generate();
        var state = Guid.NewGuid().ToString("n");

        var request = new AuthorizeRequest(
            AuthorizeEndpoint: $"{openEmr.BaseUrl.TrimEnd('/')}/oauth2/{openEmr.Site}/authorize",
            ClientId: agenda.ClientId,
            RedirectUri: BuildCallbackUri(),
            Scopes: agenda.Scopes,
            State: state,
            Pkce: pkce,
            Aud: $"{openEmr.BaseUrl.TrimEnd('/')}/apis/{openEmr.Site}/fhir",
            Launch: launchToken);

        return (AuthorizeUrlBuilder.Build(request), new PendingLaunchContext(state, pkce.CodeVerifier));
    }

    /// <summary>
    /// Completes the roster-level launch: validates <paramref name="state"/> against
    /// <paramref name="pending"/>, exchanges <paramref name="code"/> for a token, and returns the
    /// resulting agenda session. Unlike <see cref="SmartLaunchService.CompleteLaunchAsync"/>, a
    /// missing launch patient context is the expected, successful case here.
    /// </summary>
    /// <exception cref="AgendaLaunchException">
    /// The state does not match (possible CSRF), or token introspection can't establish an audited
    /// clinician identity.
    /// </exception>
    public async Task<AgendaSessionContext> CompleteLaunchAsync(
        string code, string state, PendingLaunchContext pending, CancellationToken cancellationToken)
    {
        if (!string.Equals(state, pending.State, StringComparison.Ordinal))
        {
            throw new AgendaLaunchException(
                "Agenda launch callback state did not match the pending launch - rejecting as a possible CSRF attempt.");
        }

        var openEmr = openEmrOptions.Value;
        var agenda = agendaOptions.Value;
        var token = await authClient.ExchangeAuthorizationCodeAsync(
                openEmr.Site, code, BuildCallbackUri(), agenda.ClientId, pending.CodeVerifier, agenda.ClientSecret, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(token.Patient))
        {
            AgendaLaunchServiceLog.UnexpectedPatientContext(logger);
        }

        // FR-AUTH-4: every patient-data access must be attributable to who made it. A session with
        // no clinician identity could never be audited, so it must not be allowed to start.
        var introspection = await authClient.IntrospectAsync(
                openEmr.Site, token.AccessToken, agenda.ClientId, agenda.ClientSecret, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(introspection.Subject))
        {
            AgendaLaunchServiceLog.IntrospectionMissingSubject(logger, introspection.ClientId, introspection.Active);
            throw new AgendaLaunchException(
                "Token introspection returned no subject claim - cannot establish an audited clinician " +
                "identity for this session (FR-AUTH-4).");
        }

        if (!introspection.Active)
        {
            AgendaLaunchServiceLog.IntrospectionInactive(logger, introspection.ClientId);
            throw new AgendaLaunchException(
                "Token introspection reports the token is not active - a revoked or expired token " +
                "cannot start a session (FR-AUTH-4).");
        }

        return new AgendaSessionContext(token.AccessToken, openEmr.Site, introspection.Subject);
    }

    private string BuildCallbackUri() => $"{bffOptions.Value.PublicBaseUrl.TrimEnd('/')}/agenda/callback";
}
