namespace GauntletAI.AgentForge.Verification.Rules;

/// <summary>
/// Flags a non-dihydropyridine calcium-channel blocker concurrently active with a beta-blocker -
/// both slow AV conduction, raising bradycardia/heart-block risk (ARCHITECTURE.md §9.2).
/// Illustrative drug lists, pending clinical validation before pilot (PRD.md FR-VERIF-2).
/// </summary>
public sealed class NegativelyChronotropicCombinationRule : IDomainConstraintRule
{
    /// <summary>Non-dihydropyridine CCBs only - dihydropyridines (e.g. amlodipine) are not chronotropic.</summary>
    private static readonly string[] NonDihydropyridineCcbs = ["diltiazem", "verapamil"];

    private static readonly string[] BetaBlockers =
    [
        "metoprolol", "atenolol", "carvedilol", "bisoprolol", "propranolol", "nebivolol", "labetalol",
    ];

    /// <inheritdoc />
    public string RuleId => "negatively-chronotropic-combination";

    /// <inheritdoc />
    public IReadOnlyList<DomainConstraintFlag> Evaluate(DomainConstraintInput input)
    {
        var active = input.ActiveMedications.Where(m => string.Equals(m.Status, "active", StringComparison.OrdinalIgnoreCase));

        var ccb = active.FirstOrDefault(m => NonDihydropyridineCcbs.Any(
            drug => m.MedicationDisplay.Contains(drug, StringComparison.OrdinalIgnoreCase)));
        var betaBlocker = active.FirstOrDefault(m => BetaBlockers.Any(
            drug => m.MedicationDisplay.Contains(drug, StringComparison.OrdinalIgnoreCase)));

        if (ccb is null || betaBlocker is null)
        {
            return [];
        }

        return
        [
            new DomainConstraintFlag(
                RuleId,
                $"{ccb.MedicationDisplay} (non-dihydropyridine CCB) with {betaBlocker.MedicationDisplay} " +
                "(beta-blocker): both slow AV conduction (illustrative check, pending clinical validation).",
                [ccb.Source, betaBlocker.Source]),
        ];
    }
}
