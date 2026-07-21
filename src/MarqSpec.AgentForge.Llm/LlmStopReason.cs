namespace MarqSpec.AgentForge.Llm;

/// <summary>Why the model stopped generating.</summary>
public enum LlmStopReason
{
    /// <summary>The model finished its response naturally.</summary>
    EndTurn,

    /// <summary>The model wants to call one or more tools - see <see cref="LlmResponse.ToolCalls"/>.</summary>
    ToolUse,

    /// <summary>Hit <see cref="LlmRequest.MaxOutputTokens"/> before finishing.</summary>
    MaxTokens,

    /// <summary>Stopped for a provider-specific reason not covered above.</summary>
    Other,
}
