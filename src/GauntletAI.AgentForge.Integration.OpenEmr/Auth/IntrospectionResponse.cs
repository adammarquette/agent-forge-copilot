using System.Text.Json.Serialization;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Response body from <c>POST /oauth2/{site}/introspect</c> (RFC 7662).
/// </summary>
/// <param name="Active">Whether the token is currently valid.</param>
/// <param name="Scope">Space-separated scopes granted to the token, when active.</param>
/// <param name="ClientId">The client the token was issued to, when active.</param>
/// <param name="ExpiresAtUnixSeconds">Expiry as Unix time, when active.</param>
/// <param name="Subject">The authenticated user's subject identifier, when active.</param>
/// <param name="Patient">SMART launch patient context carried by the token, when present.</param>
public sealed record IntrospectionResponse(
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("client_id")] string? ClientId,
    [property: JsonPropertyName("exp"), JsonConverter(typeof(LenientUnixSecondsConverter))] long? ExpiresAtUnixSeconds,
    [property: JsonPropertyName("sub")] string? Subject,
    [property: JsonPropertyName("patient")] string? Patient);
