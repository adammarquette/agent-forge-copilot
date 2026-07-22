using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MarqSpec.AgentForge.Llm.Anthropic;

/// <summary>
/// Wire-format request body for the Anthropic Messages API. Deliberately has no
/// <c>temperature</c> field - the real API rejects an explicit value for the current model with a
/// 400 <c>invalid_request_error</c> ("`temperature` is deprecated for this model"), confirmed live
/// against the deployed app (GitLab issue #38).
/// </summary>
public sealed record AnthropicMessageRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
    [property: JsonPropertyName("tools"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AnthropicToolDefinition>? Tools);

/// <summary>One conversation turn in Anthropic wire format.</summary>
public sealed record AnthropicMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] IReadOnlyList<AnthropicRequestContentBlock> Content);

/// <summary>
/// One outgoing content block. Polymorphic by <see cref="Type"/> (<c>text</c>, <c>tool_use</c>,
/// <c>tool_result</c>); fields that don't apply to a given type are omitted from the serialized
/// JSON entirely, not sent as explicit nulls.
/// </summary>
public sealed record AnthropicRequestContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id = null,
    [property: JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: JsonPropertyName("input"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonNode? Input = null,
    [property: JsonPropertyName("tool_use_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ToolUseId = null,
    [property: JsonPropertyName("content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ToolResultContent = null,
    [property: JsonPropertyName("is_error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsError = null,
    [property: JsonPropertyName("source"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AnthropicContentSource? Source = null);

/// <summary>Base64 binary source for an <c>image</c> or <c>document</c> content block.</summary>
public sealed record AnthropicContentSource(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("media_type")] string MediaType,
    [property: JsonPropertyName("data")] string Data);

/// <summary>One tool definition in Anthropic wire format.</summary>
public sealed record AnthropicToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("input_schema")] JsonNode InputSchema);
