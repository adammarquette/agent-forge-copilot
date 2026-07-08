using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GauntletAI.AgentForge.Llm.Anthropic;

/// <summary>Wire-format request body for the Anthropic Messages API.</summary>
public sealed record AnthropicMessageRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
    [property: JsonPropertyName("tools")] IReadOnlyList<AnthropicToolDefinition>? Tools,
    [property: JsonPropertyName("temperature")] double Temperature);

/// <summary>One conversation turn in Anthropic wire format.</summary>
public sealed record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

/// <summary>One tool definition in Anthropic wire format.</summary>
public sealed record AnthropicToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("input_schema")] JsonNode InputSchema);
