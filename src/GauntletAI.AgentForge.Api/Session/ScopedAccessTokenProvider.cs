using GauntletAI.AgentForge.Integration.OpenEmr.Http;

namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// Registered per-request/per-hub-invocation scope (never singleton): holds the current session's
/// access token for the lifetime of one call, so <see cref="AuthHandler"/> can attach it to
/// outbound FHIR calls without ever routing the token through the browser (ARCHITECTURE.md D11).
/// </summary>
public sealed class ScopedAccessTokenProvider : IScopedAccessTokenProvider
{
    /// <inheritdoc />
    public string? AccessToken { get; set; }

    /// <inheritdoc />
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(AccessToken);
}
