namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>Wire payload for a <c>"brief"</c> or <c>"answer"</c> <see cref="ChatMessage"/>.</summary>
/// <param name="Answer">The orchestrator's final text answer for the turn.</param>
public sealed record ChatAnswerPayload(string Answer);
