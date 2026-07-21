using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.Llm.Anthropic;

/// <summary>
/// Attaches the Anthropic API key and API version on every outbound call
/// (ENGINEERING_STANDARDS.md §4 - cross-cutting concerns in a DelegatingHandler, not call sites).
/// </summary>
public sealed class AnthropicAuthHandler(IOptions<LlmProviderOptions> options) : DelegatingHandler
{
    /// <summary>Anthropic Messages API version this client was built against.</summary>
    public const string AnthropicVersion = "2023-06-01";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("x-api-key");
        request.Headers.Add("x-api-key", options.Value.ApiKey);
        request.Headers.Remove("anthropic-version");
        request.Headers.Add("anthropic-version", AnthropicVersion);

        return base.SendAsync(request, cancellationToken);
    }
}
