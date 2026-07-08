namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>Wire payload for a <c>"brief"</c> or <c>"answer"</c> <see cref="ChatMessage"/>.</summary>
/// <param name="Answer">The orchestrator's final (verified) text answer for the turn.</param>
/// <param name="SafetyFlags">Cardiology domain-constraint flags raised this turn (FR-VERIF-2), if any.</param>
public sealed record ChatAnswerPayload(string Answer, IReadOnlyList<SafetyFlagPayload> SafetyFlags);
