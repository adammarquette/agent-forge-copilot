namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>Wire payload for a <c>"brief"</c> or <c>"answer"</c> <see cref="ChatMessage"/>.</summary>
/// <param name="Answer">The orchestrator's final (verified) text answer for the turn.</param>
/// <param name="SafetyFlags">Cardiology domain-constraint flags raised this turn (FR-VERIF-2), if any.</param>
/// <param name="SuppressedClaims">Draft lines removed for failing source attribution this turn (PRD.md §13.1), if any.</param>
/// <param name="IsDeterministicFallback">
/// True when <see cref="Answer"/> is raw source data rather than a model-synthesized answer (PRD.md
/// §13.1) - the UI should render this as "Summary unavailable right now - here is the source data",
/// not present it as if the model said it.
/// </param>
public sealed record ChatAnswerPayload(
    string Answer,
    IReadOnlyList<SafetyFlagPayload> SafetyFlags,
    IReadOnlyList<SuppressedClaimPayload> SuppressedClaims,
    bool IsDeterministicFallback = false);
