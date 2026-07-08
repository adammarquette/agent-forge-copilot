namespace GauntletAI.AgentForge.Llm;

/// <summary>One turn of conversation history sent to the model.</summary>
/// <param name="Role">Who spoke this turn.</param>
/// <param name="Content">The turn's text content.</param>
public sealed record LlmMessage(LlmRole Role, string Content);
