using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.Retrieval.Cohere;

/// <summary>
/// Adds the Cohere <c>Authorization: Bearer</c> header to every outbound call, reading the key from options —
/// a cross-cutting concern in a <see cref="DelegatingHandler"/>, not at the call sites (ENGINEERING_STANDARDS.md
/// §5). Only registered when a key is configured, so the key is never null here.
/// </summary>
internal sealed class CohereAuthHandler : DelegatingHandler
{
    private readonly CohereOptions _options;

    public CohereAuthHandler(IOptions<CohereOptions> options) => _options = options.Value;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
