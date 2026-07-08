namespace GauntletAI.AgentForge.Agent;

/// <summary>Result of one orchestrator turn (the initial brief or a follow-up question).</summary>
/// <param name="Answer">The model's final text answer for this turn.</param>
/// <param name="State">Updated conversation state, including this turn's messages - pass this to the next follow-up.</param>
public sealed record AgentTurnResult(string Answer, ConversationState State);
