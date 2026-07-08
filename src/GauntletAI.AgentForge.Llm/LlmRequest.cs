namespace GauntletAI.AgentForge.Llm;

/// <summary>
/// One model call. The orchestrator (Epic 5) owns the conversation loop and tool-chaining
/// decisions; this is just what gets sent for a single turn.
/// </summary>
/// <param name="SystemPrompt">The cardiology profile system prompt (ARCHITECTURE.md §8).</param>
/// <param name="Messages">Conversation history, oldest first.</param>
/// <param name="Tools">Tools the model may call this turn, when tool use is allowed.</param>
/// <param name="Temperature">Low by design (grounding discipline, ARCHITECTURE.md §8) - defaults to 0.</param>
/// <param name="MaxOutputTokens">Upper bound on the model's response length.</param>
public sealed record LlmRequest(
    string SystemPrompt,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmToolDefinition>? Tools = null,
    double Temperature = 0.0,
    int MaxOutputTokens = 4096);
