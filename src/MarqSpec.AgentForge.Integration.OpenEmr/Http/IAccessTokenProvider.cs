namespace MarqSpec.AgentForge.Integration.OpenEmr.Http;

/// <summary>
/// Resolves the current request's OpenEMR access token. The BFF (Epic 3) is the concrete
/// implementation: it holds the clinician's SMART EHR launch token server-side, keyed to the
/// browser session, and never exposes it to the browser SPA (ARCHITECTURE.md D11).
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Returns the bearer token to attach to the current outbound OpenEMR call, or
    /// <see langword="null"/> if no authenticated session is available.
    /// </summary>
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}
