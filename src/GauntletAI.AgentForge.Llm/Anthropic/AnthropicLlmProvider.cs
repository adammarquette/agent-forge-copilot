using Microsoft.Extensions.Options;

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
        var wireResponse = await api.CreateMessageAsync(wireRequest, cancellationToken).ConfigureAwait(false);

        return AnthropicResponseMapper.Map(
            wireResponse,
            options.Value.InputPricePerMillionTokensUsd,
            options.Value.OutputPricePerMillionTokensUsd);
    }
}
