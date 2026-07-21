using Refit;

namespace MarqSpec.AgentForge.Llm.Anthropic;

/// <summary>The Anthropic Messages API.</summary>
public interface IAnthropicMessagesApi
{
    /// <summary>Sends one message-creation request.</summary>
    [Post("/v1/messages")]
    Task<AnthropicMessageResponse> CreateMessageAsync(
        [Body] AnthropicMessageRequest request, CancellationToken cancellationToken = default);
}
