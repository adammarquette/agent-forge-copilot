using MarqSpec.AgentForge.Verification.Rules;

namespace MarqSpec.AgentForge.Verification;

/// <summary>The default, illustrative cardiology domain-constraint rule set (ARCHITECTURE.md §9.2).</summary>
public static class CardiologyConstraintRules
{
    /// <summary>All five illustrative rules, pending clinical validation before pilot (PRD.md FR-VERIF-2).</summary>
    public static IReadOnlyList<IDomainConstraintRule> Default { get; } =
    [
        new InrTherapeuticRangeRule(),
        new QtProlongingCombinationRule(),
        new RenalDoacDosingRule(),
        new AceiArbHyperkalemiaRule(),
        new NegativelyChronotropicCombinationRule(),
    ];
}
