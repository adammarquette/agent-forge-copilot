namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// The agent's multi-turn loop (see <see cref="AgentOrchestrator"/>). Extracted as an interface so
/// the BFF (Epic 3) can fake it in its own unit tests without driving a real LLM/tool chain -
/// the same reason <see cref="IMcpToolDispatcher"/> was extracted in Epic 5.
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>Starts a new session for a patient and generates the initial pre-visit brief (UC-1).</summary>
    Task<AgentTurnResult> StartBriefAsync(string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Asks a follow-up question within an existing session, maintaining context (UC-2).</summary>
    Task<AgentTurnResult> AskFollowUpAsync(ConversationState state, string question, CancellationToken cancellationToken);
}
