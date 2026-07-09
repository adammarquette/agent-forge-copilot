using System.Net;

namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>
/// Manually manages a <see cref="CookieContainer"/> around an in-memory <c>TestServer</c> handler,
/// which has no cookie handling of its own. Shared between a plain HTTP client and a SignalR
/// <c>HubConnection</c>'s handler so both see the same session cookie (tests/AGENTS.md - real
/// session behavior, not a mock).
/// </summary>
internal sealed class CookieContainerHandler(CookieContainer cookieContainer) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookieHeader = cookieContainer.GetCookieHeader(request.RequestUri!);
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var header in setCookieHeaders)
            {
                cookieContainer.SetCookies(request.RequestUri!, header);
            }
        }

        return response;
    }
}
