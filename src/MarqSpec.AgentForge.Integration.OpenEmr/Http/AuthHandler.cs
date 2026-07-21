using System.Net.Http.Headers;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Http;

/// <summary>
/// Attaches the clinician's bearer token to every outbound OpenEMR call
/// (ENGINEERING_STANDARDS.md §4). The token itself is never hard-coded and never logged; it is
/// resolved per-request from <see cref="IAccessTokenProvider"/>. No token means no call: FR-AUTH-1
/// requires an unauthenticated request be rejected before any tool runs, so this layer refuses to
/// send rather than letting an unauthenticated call go out for OpenEMR's own 401 to catch after
/// the fact.
/// </summary>
public sealed class AuthHandler(IAccessTokenProvider tokenProvider) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            throw new UnauthenticatedRequestException(
                "No access token is available for this request - refusing to send it unauthenticated (FR-AUTH-1).");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
