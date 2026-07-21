using System.Text.Json.Serialization;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// JSON body for <c>POST /oauth2/{site}/registration</c> (RFC 7591, INTERFACE_CONTROL.md A.2).
/// Unlike the token/introspect endpoints, dynamic client registration is JSON, not
/// form-urlencoded.
/// </summary>
public sealed record ClientRegistrationRequest(
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("grant_types")] IReadOnlyList<string> GrantTypes,
    [property: JsonPropertyName("response_types")] IReadOnlyList<string> ResponseTypes,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("scope")] string Scope);
