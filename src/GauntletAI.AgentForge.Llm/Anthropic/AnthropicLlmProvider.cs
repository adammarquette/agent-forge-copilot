using Microsoft.Extensions.Options;
using Refit;

namespace GauntletAI.AgentForge.Llm.Anthropic;

/// <summary>
/// The v1 <see cref="ILlmProvider"/> implementation (ARCHITECTURE.md D12) - Sonnet-class grounded
/// summarization via the Anthropic Messages API.
/// </summary>
public sealed class AnthropicLlmProvider(IAnthropicMessagesApi api, IOptions<LlmProviderOptions> options)
    : ILlmProvider
{
    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var wireRequest = AnthropicRequestMapper.Map(request, options.Value.Model);
        AnthropicMessageResponse wireResponse;
        try
        {
            wireResponse = await api.CreateMessageAsync(wireRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (!string.IsNullOrEmpty(ex.Content))
        {
            // ApiException.Message is just the status line - the actual reason (Anthropic's error
            // body) lives in .Content. AgentOrchestrator logs whatever this exception's Message is
            // verbatim on fallback (ARCHITECTURE.md §13.1 - never fail silently), so folding the
            // body in here is what makes a real invalid-request failure diagnosable at all; kept a
            // plain HttpRequestException rather than a Refit type so ILlmProvider stays
            // provider/transport-agnostic for whoever catches it upstream.
            throw new HttpRequestException($"{ex.Message} {ex.Content}", ex, ex.StatusCode);
        }

        return AnthropicResponseMapper.Map(
            wireResponse,
            options.Value.InputPricePerMillionTokensUsd,
            options.Value.OutputPricePerMillionTokensUsd);
    }
}
