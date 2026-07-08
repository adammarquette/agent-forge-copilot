using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.Agent;

/// <summary>Result of one orchestrator turn (the initial brief or a follow-up question).</summary>
/// <param name="Answer">The model's final text answer for this turn, after the verification gate (FR-VERIF-0).</param>
/// <param name="State">Updated conversation state, including this turn's messages - pass this to the next follow-up.</param>
/// <param name="SafetyFlags">Cardiology domain-constraint flags raised this turn (FR-VERIF-2), to surface alongside the answer.</param>
public sealed record AgentTurnResult(string Answer, ConversationState State, IReadOnlyList<DomainConstraintFlag> SafetyFlags);
