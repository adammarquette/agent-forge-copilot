using System.Text.Json.Serialization;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Response body from <c>POST /oauth2/{site}/token</c> (INTERFACE_CONTROL.md A.3). Claims
/// encode the authenticated user, granted scopes, and launch patient context.
/// </summary>
/// <param name="AccessToken">Bearer token attached to every subsequent FHIR call.</param>
/// <param name="TokenType">Always <c>Bearer</c> for this flow.</param>
/// <param name="ExpiresIn">Access token lifetime in seconds, when advertised.</param>
/// <param name="Scope">Space-separated scopes actually granted (may narrow what was requested).</param>
/// <param name="RefreshToken">Present only when <c>offline_access</c> was granted.</param>
/// <param name="Patient">SMART launch patient context, when the launch was patient-scoped.</param>
/// <param name="IdToken">OIDC id_token, when the <c>openid</c> scope was granted.</param>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("patient")] string? Patient,
    [property: JsonPropertyName("id_token")] string? IdToken);
