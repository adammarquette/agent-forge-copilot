using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.Verification;

/// <summary>A triggered cardiology domain-constraint violation (FR-VERIF-2).</summary>
/// <param name="RuleId">Stable identifier of the rule that fired, e.g. <c>"inr-therapeutic-range"</c>.</param>
/// <param name="Description">Clinician-facing explanation naming the rule and the values involved.</param>
/// <param name="Sources">The specific resources the flag is grounded in.</param>
public sealed record DomainConstraintFlag(string RuleId, string Description, IReadOnlyList<ClinicalSourceRef> Sources);
