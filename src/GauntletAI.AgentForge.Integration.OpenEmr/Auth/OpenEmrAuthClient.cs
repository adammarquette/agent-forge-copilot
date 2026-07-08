namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Orchestrates the SMART EHR launch token exchange (INTERFACE_CONTROL.md Interface A.3) on top
/// of the raw <see cref="IOpenEmrAuthApi"/> endpoint.
/// </summary>
public sealed class OpenEmrAuthClient(IOpenEmrAuthApi api)
{
    /// <summary>
    /// Exchanges an authorization code for an access token using the authorization_code grant
    /// with PKCE. <paramref name="clientSecret"/> is omitted (public client) unless the
    /// registered client is confidential.
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
            ClientSecret = clientSecret,
        };

        return api.ExchangeTokenAsync(site, request, cancellationToken);
    }

    /// <summary>Validates <paramref name="accessToken"/> and returns its active claims.</summary>
    public Task<IntrospectionResponse> IntrospectAsync(
        string site, string accessToken, CancellationToken cancellationToken)
    {
        var request = new IntrospectionRequest { Token = accessToken, TokenTypeHint = "access_token" };
        return api.IntrospectAsync(site, request, cancellationToken);
    }

    /// <summary>
    /// Registers the sidecar as a public OAuth2 client (token_endpoint_auth_method "none" - PKCE
    /// stands in for a client secret, since a browser-launched public client can't keep one).
    /// Fixes grant_types to authorization_code + refresh_token and response_types to code; a
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
            TokenEndpointAuthMethod: "none",
            Scope: string.Join(' ', scopes));

        return api.RegisterClientAsync(site, request, cancellationToken);
    }
}
