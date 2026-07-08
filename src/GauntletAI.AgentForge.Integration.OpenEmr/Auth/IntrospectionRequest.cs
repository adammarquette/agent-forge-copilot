using Refit;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Form-urlencoded body for <c>POST /oauth2/{site}/introspect</c> (INTERFACE_CONTROL.md A.2,
/// RFC 7662).
/// </summary>
public sealed record IntrospectionRequest
{
    /// <summary>The token to validate.</summary>
    [AliasAs("token")]
    public required string Token { get; init; }

    /// <summary>Hints the token type to the server; this sidecar only introspects access tokens.</summary>
    [AliasAs("token_type_hint")]
    public string? TokenTypeHint { get; init; }
}
