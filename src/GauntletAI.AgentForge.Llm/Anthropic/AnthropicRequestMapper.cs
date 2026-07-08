using System.Text.Json.Nodes;

namespace GauntletAI.AgentForge.Llm.Anthropic;

/// <summary>Maps the provider-agnostic <see cref="LlmRequest"/> to Anthropic wire format.</summary>
public static class AnthropicRequestMapper
{
    /// <summary>Maps <paramref name="request"/>, using <paramref name="model"/> as the wire model identifier.</summary>
    public static AnthropicMessageRequest Map(LlmRequest request, string model) => new(
        Model: model,
        MaxTokens: request.MaxOutputTokens,
        System: request.SystemPrompt,
        Messages: [.. request.Messages.Select(m => new AnthropicMessage(MapRole(m.Role), m.Content))],
        Tools: request.Tools is null ? null : [.. request.Tools.Select(MapTool)],
        Temperature: request.Temperature);

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown LlmRole."),
    };

    private static AnthropicToolDefinition MapTool(LlmToolDefinition tool) => new(
        Name: tool.Name,
        Description: tool.Description,
        InputSchema: JsonNode.Parse(tool.InputJsonSchema)
            ?? throw new InvalidOperationException($"Tool '{tool.Name}' has a null InputJsonSchema."));
}
