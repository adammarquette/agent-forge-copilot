namespace MarqSpec.AgentForge.Verification;

/// <summary>
/// One cardiology domain constraint (ARCHITECTURE.md §9.2). Each implementation is a small,
/// reviewable, versioned rule - illustrative and pending clinical validation before pilot
/// (PRD.md FR-VERIF-2), not a claim of clinical completeness or correctness.
/// </summary>
public interface IDomainConstraintRule
{
    /// <summary>Stable identifier for this rule, used as <see cref="DomainConstraintFlag.RuleId"/>.</summary>
    string RuleId { get; }

    /// <summary>Evaluates <paramref name="input"/> and returns every violation found (zero or more).</summary>
    IReadOnlyList<DomainConstraintFlag> Evaluate(DomainConstraintInput input);
}
