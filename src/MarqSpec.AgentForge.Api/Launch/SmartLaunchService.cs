using MarqSpec.AgentForge.Api.Session;
using MarqSpec.AgentForge.Integration.OpenEmr;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.Api.Launch;

/// <summary>
/// Drives the SMART EHR launch authorization-code flow end to end (INTERFACE_CONTROL.md A.3):
/// starts the redirect to OpenEMR's authorize endpoint, then completes the callback by validating
/// state and exchanging the code for a token - producing the <see cref="PatientSessionContext"/>
/// the BFF holds server-side for the rest of the session (ARCHITECTURE.md D11). Kept independent
/// of <see cref="Microsoft.AspNetCore.Http.HttpContext"/> so it is directly unit-testable; the
/// minimal-API endpoints are the thin layer that reads/writes the session around it.
/// </summary>
public sealed class SmartLaunchService(
    IOpenEmrAuthClient authClient,
    IOptions<OpenEmrOptions> openEmrOptions,
    IOptions<BffOptions> bffOptions,
    ILogger<SmartLaunchService> logger)
{
    /// <summary>
    /// Builds the authorize redirect for a launch carrying <paramref name="launchToken"/> (the
    /// SMART <c>launch</c> parameter). Returns the URL to redirect the browser to, and the pending
    /// context the caller must persist (session) until the callback arrives.
    /// </summary>
    public (Uri AuthorizeUrl, PendingLaunchContext Pending) BeginLaunch(string? launchToken)
    {
        var options = openEmrOptions.Value;
        var pkce = PkceGenerator.Generate();
        var state = Guid.NewGuid().ToString("n");

        var request = new AuthorizeRequest(
            AuthorizeEndpoint: $"{options.BaseUrl.TrimEnd('/')}/oauth2/{options.Site}/authorize",
            ClientId: options.ClientId,
            RedirectUri: BuildCallbackUri(),
            Scopes: options.Scopes,
            State: state,
            Pkce: pkce,
            // The FHIR base, not the bare server base - OpenEMR rejects the latter with
            // "invalid_request - Aud parameter did not match authorized server" (confirmed live
            // against the QA server; masked until now behind a never-registered OpenEmr__ClientId).
            Aud: $"{options.BaseUrl.TrimEnd('/')}/apis/{options.Site}/fhir",
            Launch: launchToken);

        return (AuthorizeUrlBuilder.Build(request), new PendingLaunchContext(state, pkce.CodeVerifier));
    }

    /// <summary>
    /// Completes the launch: validates <paramref name="state"/> against <paramref name="pending"/>,
    /// exchanges <paramref name="code"/> for a token, and returns the resulting patient session.
    /// </summary>
    /// <exception cref="SmartLaunchException">
    /// The state does not match (possible CSRF), or the token response carries no launch patient
    /// context.
    /// </exception>
    public async Task<PatientSessionContext> CompleteLaunchAsync(
        string code, string state, PendingLaunchContext pending, CancellationToken cancellationToken)
    {
        if (!string.Equals(state, pending.State, StringComparison.Ordinal))
        {
            throw new SmartLaunchException(
                "SMART launch callback state did not match the pending launch - rejecting as a possible CSRF attempt.");
        }

        var options = openEmrOptions.Value;
        var token = await authClient.ExchangeAuthorizationCodeAsync(
            options.Site, code, BuildCallbackUri(), options.ClientId, pending.CodeVerifier, options.ClientSecret, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(token.Patient))
        {
            throw new SmartLaunchException(
                "Token response carried no launch patient context - this product is single-patient-scoped " +
                "and cannot proceed without one (INTERFACE_CONTROL.md A.3).");
        }

        // FR-AUTH-4: every patient-data access must be attributable to who made it. A session with
        // no clinician identity could never be audited, so it must not be allowed to start.
        var introspection = await authClient.IntrospectAsync(
                options.Site, token.AccessToken, options.ClientId, options.ClientSecret, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(introspection.Subject))
        {
            SmartLaunchServiceLog.IntrospectionMissingSubject(logger, introspection.ClientId, introspection.Active);
            throw new SmartLaunchException(
                "Token introspection returned no subject claim - cannot establish an audited clinician " +
                "identity for this session (FR-AUTH-4).");
        }

        if (!introspection.Active)
        {
            SmartLaunchServiceLog.IntrospectionInactive(logger, introspection.ClientId);
            throw new SmartLaunchException(
                "Token introspection reports the token is not active - a revoked or expired token " +
                "cannot start a session (FR-AUTH-4).");
        }

        return new PatientSessionContext(token.AccessToken, options.Site, token.Patient, introspection.Subject);
    }

    private string BuildCallbackUri() => $"{bffOptions.Value.PublicBaseUrl.TrimEnd('/')}{bffOptions.Value.CallbackPath}";
}
