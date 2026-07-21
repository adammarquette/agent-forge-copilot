namespace MarqSpec.AgentForge.Llm;

/// <summary>One turn of conversation history sent to the model.</summary>
/// <param name="Role">Who spoke this turn.</param>
/// <param name="Content">
/// One or more content blocks - usually a single <see cref="LlmTextContent"/>, but a tool-calling
/// turn carries <see cref="LlmToolUseContent"/> and its answering
/// <see cref="LlmToolResultContent"/>.
/// </param>
public sealed record LlmMessage(LlmRole Role, IReadOnlyList<LlmContent> Content)
{
    /// <summary>Convenience factory for the common case of a single plain-text turn.</summary>
    public static LlmMessage FromText(LlmRole role, string text) => new(role, [new LlmTextContent(text)]);
}
