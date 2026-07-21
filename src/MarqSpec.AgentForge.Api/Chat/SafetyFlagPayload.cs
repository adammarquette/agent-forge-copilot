namespace MarqSpec.AgentForge.Api.Chat;

/// <summary>Wire shape for one <c>DomainConstraintFlag</c> (ARCHITECTURE.md's request-flow: safety flags travel with the brief, not just into a log).</summary>
/// <param name="RuleId">Stable rule identifier, e.g. <c>"inr-therapeutic-range"</c>.</param>
/// <param name="Description">Clinician-facing explanation of the flag.</param>
/// <param name="Citations">The <c>{ResourceType}/{Id}</c> citations the flag is grounded in.</param>
public sealed record SafetyFlagPayload(string RuleId, string Description, IReadOnlyList<string> Citations);
