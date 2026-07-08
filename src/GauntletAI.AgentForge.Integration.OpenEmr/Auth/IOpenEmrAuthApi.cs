using Refit;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// OAuth2/SMART authorization endpoints (INTERFACE_CONTROL.md Interface A.2). The
/// <c>/authorize</c> endpoint is a browser redirect, not an API call, and so has no member here -
/// see <see cref="AuthorizeUrlBuilder"/>.
/// </summary>
public interface IOpenEmrAuthApi
{
    /// <summary>Exchanges an authorization code (or refresh token) for an access token.</summary>
    [Post("/oauth2/{site}/token")]
    Task<TokenResponse> ExchangeTokenAsync(
        string site,
        [Body(BodySerializationMethod.UrlEncoded)] TokenRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Validates a token and returns its active claims (RFC 7662).</summary>
    [Post("/oauth2/{site}/introspect")]
    Task<IntrospectionResponse> IntrospectAsync(
        string site,
        [Body(BodySerializationMethod.UrlEncoded)] IntrospectionRequest request,
        CancellationToken cancellationToken = default);
}
