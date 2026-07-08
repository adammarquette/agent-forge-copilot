namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Orchestrates the SMART EHR launch token exchange (see <see cref="OpenEmrAuthClient"/>).
/// Extracted as an interface so the BFF's SMART launch flow (Epic 3) can fake the token exchange
/// in its own unit tests without a real OpenEMR authorization server behind it - the same reason
/// <c>IOpenEmrFhirClient</c> was extracted in Epic 2.
/// </summary>
public interface IOpenEmrAuthClient
{
    /// <summary>
    /// Exchanges an authorization code for an access token using the authorization_code grant
    /// with PKCE. <paramref name="clientSecret"/> is omitted (public client) unless the
    /// registered client is confidential.
    /// </summary>
    Task<TokenResponse> ExchangeAuthorizationCodeAsync(
        string site,
        string code,
        string redirectUri,
        string clientId,
        string codeVerifier,
        string? clientSecret,
        CancellationToken cancellationToken);

    /// <summary>Validates <paramref name="accessToken"/> and returns its active claims.</summary>
    Task<IntrospectionResponse> IntrospectAsync(string site, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    /// Registers the sidecar as a public OAuth2 client (token_endpoint_auth_method "none" - PKCE
    /// stands in for a client secret, since a browser-launched public client can't keep one).
    /// Fixes grant_types to authorization_code + refresh_token and response_types to code; a
    /// confidential-client registration path is not implemented (D12/v1 scope - one auth model).
    /// </summary>
    Task<ClientRegistrationResponse> RegisterPublicClientAsync(
        string site,
        string clientName,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken);
}
