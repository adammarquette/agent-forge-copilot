namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Parameters for the SMART EHR launch authorization-code request
/// (INTERFACE_CONTROL.md Interface A.3).
/// </summary>
/// <param name="AuthorizeEndpoint">The <c>authorize</c> endpoint from SMART/OIDC discovery.</param>
/// <param name="ClientId">The sidecar's registered OAuth2 client id.</param>
/// <param name="RedirectUri">Must match a URI registered for <paramref name="ClientId"/>.</param>
/// <param name="Scopes">Least-privilege scopes requested (INTERFACE_CONTROL.md A.4).</param>
/// <param name="State">Opaque CSRF-protection value, verified when the callback returns.</param>
/// <param name="Pkce">The PKCE pair for this launch; <see cref="PkceChallenge.CodeVerifier"/> is held
/// server-side and exchanged with the authorization code, never sent on this request.</param>
/// <param name="Aud">SMART v2 resource-server audience, when required by the authorization server.</param>
/// <param name="Launch">The SMART EHR launch token, when launched in-context from OpenEMR.</param>
public sealed record AuthorizeRequest(
    string AuthorizeEndpoint,
    string ClientId,
    string RedirectUri,
    IReadOnlyList<string> Scopes,
    string State,
    PkceChallenge Pkce,
    string? Aud = null,
    string? Launch = null);
