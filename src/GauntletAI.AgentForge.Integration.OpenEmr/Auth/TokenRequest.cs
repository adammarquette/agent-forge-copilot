using Refit;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Form-urlencoded body for <c>POST /oauth2/{site}/token</c> (INTERFACE_CONTROL.md A.2).
/// </summary>
public sealed record TokenRequest
{
    /// <summary>OAuth2 grant type, e.g. <c>authorization_code</c> or <c>refresh_token</c>.</summary>
    [AliasAs("grant_type")]
    public required string GrantType { get; init; }

    /// <summary>The authorization code from the callback, for the authorization_code grant.</summary>
    [AliasAs("code")]
    public string? Code { get; init; }

    /// <summary>Must match the redirect_uri used on the original authorize request.</summary>
    [AliasAs("redirect_uri")]
    public string? RedirectUri { get; init; }

    /// <summary>The sidecar's registered OAuth2 client id.</summary>
    [AliasAs("client_id")]
    public required string ClientId { get; init; }

    /// <summary>The PKCE code_verifier matching the code_challenge sent on the authorize request.</summary>
    [AliasAs("code_verifier")]
    public string? CodeVerifier { get; init; }

    /// <summary>The refresh token being redeemed, for the refresh_token grant.</summary>
    [AliasAs("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>Present only for a confidential client; a public client omits this entirely.</summary>
    [AliasAs("client_secret")]
    public string? ClientSecret { get; init; }
}
