namespace MarqSpec.AgentForge.Llm;

/// <summary>The model's response to one <see cref="LlmRequest"/>.</summary>
/// <param name="Content">Text content, when the model produced any (may be empty on pure tool-use turns).</param>
/// <param name="ToolCalls">Tools the model wants invoked this turn; empty when it didn't ask for any.</param>
/// <param name="StopReason">Why generation stopped.</param>
/// <param name="Usage">Token/cost accounting for this call.</param>
public sealed record LlmResponse(
    string Content,
    IReadOnlyList<LlmToolCall> ToolCalls,
    LlmStopReason StopReason,
    LlmUsage Usage);
