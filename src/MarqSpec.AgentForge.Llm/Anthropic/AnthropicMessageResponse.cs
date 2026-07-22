using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MarqSpec.AgentForge.Llm.Anthropic;

/// <summary>Wire-format response body from the Anthropic Messages API.</summary>
public sealed record AnthropicMessageResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentBlock> Content,
    [property: JsonPropertyName("stop_reason")] string? StopReason,
    [property: JsonPropertyName("usage")] AnthropicUsage Usage);

/// <summary>
/// One content block. Anthropic's content array is polymorphic by <see cref="Type"/>
/// (<c>text</c> or <c>tool_use</c>); the fields that don't apply to a given type are null.
/// </summary>
public sealed record AnthropicContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("input")] JsonNode? Input);

/// <summary>Token accounting from the Anthropic Messages API.</summary>
public sealed record AnthropicUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens);
