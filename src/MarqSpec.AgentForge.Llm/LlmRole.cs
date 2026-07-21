namespace MarqSpec.AgentForge.Llm;

/// <summary>Speaker role for one turn in an <see cref="LlmRequest"/>'s conversation history.</summary>
public enum LlmRole
{
    /// <summary>The clinician (via the sidecar) or a tool result being handed back to the model.</summary>
    User,

    /// <summary>A prior model turn, including any tool calls it made.</summary>
    Assistant,
}
