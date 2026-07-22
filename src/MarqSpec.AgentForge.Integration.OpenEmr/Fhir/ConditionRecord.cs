namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// A cardiology problem, mapped from a FHIR <c>Condition</c> (INTERFACE_CONTROL.md §B.1).
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="ProblemDisplay">Human-readable problem name, e.g. "Atrial fibrillation".</param>
/// <param name="ClinicalStatus">FHIR <c>Condition.clinicalStatus</c> code (active, resolved, ...).</param>
/// <param name="OnsetDate">When the condition began, when present.</param>
public sealed record ConditionRecord(
    ClinicalSourceRef Source,
    string ProblemDisplay,
    string ClinicalStatus,
    DateTimeOffset? OnsetDate);
