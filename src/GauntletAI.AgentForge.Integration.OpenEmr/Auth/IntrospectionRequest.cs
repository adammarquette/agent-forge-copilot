using Refit;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Form-urlencoded body for <c>POST /oauth2/{site}/introspect</c> (INTERFACE_CONTROL.md A.2,
/// RFC 7662). OpenEMR requires the caller to authenticate as a registered client via
/// <see cref="ClientId"/>/<see cref="ClientSecret"/> form parameters - confirmed against the real
/// QA server, which rejects both an unauthenticated call and HTTP Basic Auth with
/// "Not a registered client" (401) before it will evaluate the token itself.
/// </summary>
public sealed record IntrospectionRequest
{
    /// <summary>The token to validate.</summary>
    [AliasAs("token")]
    public required string Token { get; init; }

    /// <summary>Hints the token type to the server; this sidecar only introspects access tokens.</summary>
    [AliasAs("token_type_hint")]
    public string? TokenTypeHint { get; init; }

    /// <summary>The sidecar's registered OAuth2 client id, required to authenticate this call.</summary>
    [AliasAs("client_id")]
    public required string ClientId { get; init; }

    /// <summary>
    /// Required even for a public client - OpenEMR's registration issues an empty-string secret
    /// for those, and authenticates the introspecting caller by matching it exactly. Omitting this
    /// field (nullable here only so callers can pass through an unconfigured value; always coerced
    /// to <see cref="string.Empty"/> before this record is built - see
    /// <see cref="OpenEmrAuthClient.IntrospectAsync"/>) is treated as an unauthenticated call and
    /// silently returns <c>{"active":false}</c> rather than erroring - confirmed live.
    /// </summary>
    [AliasAs("client_secret")]
    public string? ClientSecret { get; init; }
}
