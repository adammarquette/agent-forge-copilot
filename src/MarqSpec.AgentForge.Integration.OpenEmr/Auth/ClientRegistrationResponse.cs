using System.Text.Json.Serialization;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Auth;

/// <summary>Response body from <c>POST /oauth2/{site}/registration</c> (RFC 7591).</summary>
/// <param name="ClientId">The assigned OAuth2 client id - persist this for future launches.</param>
/// <param name="ClientSecret">Present only if the authorization server issued a confidential client.</param>
/// <param name="ClientName">Echoes the registered client name.</param>
/// <param name="RedirectUris">Echoes the registered redirect URIs.</param>
public sealed record ClientRegistrationResponse(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_secret")] string? ClientSecret,
    [property: JsonPropertyName("client_name")] string? ClientName,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string>? RedirectUris);
