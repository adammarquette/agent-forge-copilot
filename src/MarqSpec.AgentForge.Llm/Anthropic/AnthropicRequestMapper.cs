using System.Text.Json.Nodes;

namespace MarqSpec.AgentForge.Llm.Anthropic;

/// <summary>Maps the provider-agnostic <see cref="LlmRequest"/> to Anthropic wire format.</summary>
public static class AnthropicRequestMapper
{
    /// <summary>Maps <paramref name="request"/>, using <paramref name="model"/> as the wire model identifier.</summary>
    public static AnthropicMessageRequest Map(LlmRequest request, string model) => new(
        Model: model,
        MaxTokens: request.MaxOutputTokens,
        System: request.SystemPrompt,
        Messages: [.. request.Messages.Select(MapMessage)],
        Tools: request.Tools is null ? null : [.. request.Tools.Select(MapTool)]);

    private static AnthropicMessage MapMessage(LlmMessage message) =>
        new(MapRole(message.Role), [.. message.Content.Select(MapContent)]);

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown LlmRole."),
    };

    private static AnthropicRequestContentBlock MapContent(LlmContent content) => content switch
    {
        LlmTextContent text => new AnthropicRequestContentBlock(Type: "text", Text: text.Text),
        LlmToolUseContent toolUse => new AnthropicRequestContentBlock(
            Type: "tool_use",
            Id: toolUse.Id,
            Name: toolUse.ToolName,
            Input: JsonNode.Parse(toolUse.ArgumentsJson)
                ?? throw new InvalidOperationException($"Tool use '{toolUse.Id}' has a null ArgumentsJson.")),
        LlmToolResultContent toolResult => new AnthropicRequestContentBlock(
            Type: "tool_result",
            ToolUseId: toolResult.ToolUseId,
            ToolResultContent: toolResult.ResultJson,
            IsError: toolResult.IsError),
        LlmImageContent image => new AnthropicRequestContentBlock(
            Type: "image",
            Source: new AnthropicContentSource("base64", image.MediaType, image.Base64Data)),
        LlmDocumentContent document => new AnthropicRequestContentBlock(
            Type: "document",
            Source: new AnthropicContentSource("base64", document.MediaType, document.Base64Data)),
        _ => throw new ArgumentOutOfRangeException(nameof(content), content, "Unknown LlmContent type."),
    };

    private static AnthropicToolDefinition MapTool(LlmToolDefinition tool) => new(
        Name: tool.Name,
        Description: tool.Description,
        InputSchema: JsonNode.Parse(tool.InputJsonSchema)
            ?? throw new InvalidOperationException($"Tool '{tool.Name}' has a null InputJsonSchema."));
}
