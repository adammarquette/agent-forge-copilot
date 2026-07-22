using MarqSpec.AgentForge.Llm;

namespace MarqSpec.AgentForge.Agent;

/// <summary>
/// One patient session's conversation history. Site and PatientId are fixed for the life of the
/// state - they are set once from the authenticated launch context and never change turn to
/// turn, which is what makes patient-scoping (FR-CHAT-3) structural rather than something each
/// call has to re-check.
/// </summary>
/// <param name="Site">OpenEMR multi-site segment for this session.</param>
/// <param name="PatientId">The one patient this entire session is scoped to.</param>
/// <param name="Messages">Conversation history so far, oldest first.</param>
public sealed record ConversationState(string Site, string PatientId, IReadOnlyList<LlmMessage> Messages)
{
    /// <summary>A fresh session for a patient, with no history yet.</summary>
    public static ConversationState Start(string site, string patientId) => new(site, patientId, []);
}
