namespace GauntletAI.AgentForge.Llm.Anthropic;

/// <summary>Maps an Anthropic wire-format response to the provider-agnostic <see cref="LlmResponse"/>.</summary>
public static class AnthropicResponseMapper
{
    private const string TextBlockType = "text";
    private const string ToolUseBlockType = "tool_use";
    private const decimal TokensPerMillion = 1_000_000m;

    /// <summary>
    /// Maps <paramref name="response"/>, computing <see cref="LlmUsage.EstimatedCostUsd"/> from
    /// the caller-supplied per-million-token pricing (LlmProviderOptions - configured, not
    /// hard-coded here).
    /// </summary>
    public static LlmResponse Map(
        AnthropicMessageResponse response, decimal inputPricePerMillion, decimal outputPricePerMillion)
    {
        var content = string.Concat(
            response.Content.Where(b => b.Type == TextBlockType).Select(b => b.Text));

        var toolCalls = response.Content
            .Where(b => b.Type == ToolUseBlockType)
            .Select(b => new LlmToolCall(
                b.Id ?? throw new InvalidOperationException("Anthropic tool_use block is missing its id."),
                b.Name ?? throw new InvalidOperationException("Anthropic tool_use block is missing its name."),
                b.Input?.ToJsonString() ?? "{}"))
            .ToList();

        var cost = response.Usage.InputTokens / TokensPerMillion * inputPricePerMillion
            + response.Usage.OutputTokens / TokensPerMillion * outputPricePerMillion;

        return new LlmResponse(
            Content: content,
            ToolCalls: toolCalls,
            StopReason: MapStopReason(response.StopReason),
            Usage: new LlmUsage(response.Usage.InputTokens, response.Usage.OutputTokens, cost));
    }

    private static LlmStopReason MapStopReason(string? stopReason) => stopReason switch
    {
        "end_turn" => LlmStopReason.EndTurn,
        "tool_use" => LlmStopReason.ToolUse,
        "max_tokens" => LlmStopReason.MaxTokens,
        _ => LlmStopReason.Other,
    };
}
