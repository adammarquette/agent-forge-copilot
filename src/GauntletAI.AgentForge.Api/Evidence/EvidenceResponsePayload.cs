namespace GauntletAI.AgentForge.Api.Evidence;

/// <summary>Response for <c>POST /evidence/ask</c>: the verified answer plus the inspectable trace.</summary>
/// <param name="Answer">The verified, cited answer (uncited claims already suppressed).</param>
/// <param name="Handoffs">The ordered graph handoffs — the inspectable routing trace.</param>
/// <param name="Evidence">Guideline snippets retrieved this turn.</param>
/// <param name="SafetyFlagCount">Number of cardiology domain-constraint flags the critic surfaced.</param>
/// <param name="SuppressedClaimCount">Number of claims the critic suppressed for failing attribution.</param>
/// <param name="ExtractedFacts">The extracted-facts JSON used this turn; null if no document was extracted.</param>
public sealed record EvidenceResponsePayload(
    string Answer,
    IReadOnlyList<HandoffPayload> Handoffs,
    IReadOnlyList<EvidencePayload> Evidence,
    int SafetyFlagCount,
    int SuppressedClaimCount,
    string? ExtractedFacts);

/// <summary>One handoff in the graph's routing trace.</summary>
public sealed record HandoffPayload(string From, string To, string Reason);

/// <summary>One retrieved guideline snippet.</summary>
public sealed record EvidencePayload(string DocumentId, string Section, string ChunkId, string Text, double Score);
