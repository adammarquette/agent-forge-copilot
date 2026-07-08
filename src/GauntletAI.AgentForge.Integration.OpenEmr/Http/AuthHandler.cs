using System.Net.Http.Headers;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Http;

/// <summary>
/// Attaches the clinician's bearer token to every outbound OpenEMR call
/// (ENGINEERING_STANDARDS.md §4). The token itself is never hard-coded and never logged; it is
/// resolved per-request from <see cref="IAccessTokenProvider"/>.
/// </summary>
public sealed class AuthHandler(IAccessTokenProvider tokenProvider) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
