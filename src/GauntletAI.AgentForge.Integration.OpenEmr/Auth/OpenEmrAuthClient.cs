namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Orchestrates the SMART EHR launch token exchange (INTERFACE_CONTROL.md Interface A.3) on top
/// of the raw <see cref="IOpenEmrAuthApi"/> endpoint.
/// </summary>
public sealed class OpenEmrAuthClient(IOpenEmrAuthApi api) : IOpenEmrAuthClient
{
    /// <summary>
    /// Exchanges an authorization code for an access token using the authorization_code grant
    /// with PKCE.
    /// </summary>
    public Task<TokenResponse> ExchangeAuthorizationCodeAsync(
        string site,
        string code,
        string redirectUri,
        string clientId,
        string codeVerifier,
        string? clientSecret,
        CancellationToken cancellationToken)
    {
        var request = new TokenRequest
        {
            GrantType = "authorization_code",
            Code = code,
            RedirectUri = redirectUri,
            ClientId = clientId,
            CodeVerifier = codeVerifier,
            // Same reasoning as IntrospectAsync below: a public client's registered secret is an
            // empty string, not absent - never let a null reach Refit's UrlEncoded serializer,
            // which would omit the field entirely instead of sending client_secret=.
            ClientSecret = clientSecret ?? string.Empty,
        };

        return api.ExchangeTokenAsync(site, request, cancellationToken);
    }

    /// <summary>
    /// Redeems a refresh token (issued when the original login requested <c>offline_access</c>) for a
    /// fresh access token, using the refresh_token grant - avoids repeating an interactive browser
    /// login every time a previously-minted, patient-scoped token expires.
    /// </summary>
    public Task<TokenResponse> RefreshAccessTokenAsync(
        string site, string refreshToken, string clientId, string? clientSecret, CancellationToken cancellationToken)
    {
        var request = new TokenRequest
        {
            GrantType = "refresh_token",
            RefreshToken = refreshToken,
            ClientId = clientId,
            ClientSecret = clientSecret,
        };

        return api.ExchangeTokenAsync(site, request, cancellationToken);
    }

    /// <summary>
    /// Validates <paramref name="accessToken"/> and returns its active claims. See
    /// <see cref="IntrospectionRequest"/> for why <paramref name="clientId"/>/<paramref name="clientSecret"/>
    /// are required.
    /// </summary>
    public Task<IntrospectionResponse> IntrospectAsync(
        string site, string accessToken, string clientId, string? clientSecret, CancellationToken cancellationToken)
    {
        var request = new IntrospectionRequest
        {
            Token = accessToken,
            TokenTypeHint = "access_token",
            ClientId = clientId,
            // A public client's registered secret is an empty string, not absent - the server
            // authenticates the caller by matching client_secret exactly (confirmed live: omitting
            // the form field entirely is treated as an unauthenticated call and silently returns
            // {"active":false} rather than an error). A null here must never reach Refit's
            // UrlEncoded body serializer, which omits null properties - coerce to string.Empty so
            // the form still carries client_secret=.
            ClientSecret = clientSecret ?? string.Empty,
        };
        return api.IntrospectAsync(site, request, cancellationToken);
    }

    /// <summary>
    /// Registers the sidecar as a public OAuth2 client. Requests token_endpoint_auth_method
    /// "client_secret_post" rather than "none": OpenEMR's registration endpoint rejects "none"
    /// outright ("Unsupported token_endpoint_auth_method value : none", confirmed against the real
    /// QA server), and issues an empty-string client_secret back for public clients regardless of
    /// the auth method requested - PKCE is still what actually secures the exchange. Fixes
    /// grant_types to authorization_code + refresh_token and response_types to code; a
    /// confidential-client registration path is not implemented (D12/v1 scope - one auth model).
    /// </summary>
    public Task<ClientRegistrationResponse> RegisterPublicClientAsync(
        string site,
        string clientName,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        var request = new ClientRegistrationRequest(
            ClientName: clientName,
            RedirectUris: redirectUris,
            GrantTypes: ["authorization_code", "refresh_token"],
            ResponseTypes: ["code"],
            TokenEndpointAuthMethod: "client_secret_post",
            Scope: string.Join(' ', scopes));

        return api.RegisterClientAsync(site, request, cancellationToken);
    }
}
