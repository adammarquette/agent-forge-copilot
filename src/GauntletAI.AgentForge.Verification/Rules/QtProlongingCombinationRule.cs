namespace GauntletAI.AgentForge.Verification.Rules;

/// <summary>
/// Flags two or more concurrently active QT-prolonging medications (ARCHITECTURE.md §9.2).
/// Illustrative drug list, pending clinical validation before pilot (PRD.md FR-VERIF-2).
/// </summary>
public sealed class QtProlongingCombinationRule : IDomainConstraintRule
{
    /// <summary>Reviewable, versioned list of QT-prolonging medication name fragments.</summary>
    private static readonly string[] QtProlongingDrugs =
    [
        "amiodarone", "sotalol", "dofetilide", "azithromycin", "erythromycin", "ondansetron",
        "haloperidol", "citalopram", "methadone", "fluconazole",
    ];

    /// <inheritdoc />
    public string RuleId => "qt-prolonging-combination";

    /// <inheritdoc />
    public IReadOnlyList<DomainConstraintFlag> Evaluate(DomainConstraintInput input)
    {
        var matches = input.ActiveMedications
            .Where(m => string.Equals(m.Status, "active", StringComparison.OrdinalIgnoreCase))
            .Where(m => QtProlongingDrugs.Any(drug => m.MedicationDisplay.Contains(drug, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matches.Count < 2)
        {
            return [];
        }

        var names = string.Join(", ", matches.Select(m => m.MedicationDisplay));
        return
        [
            new DomainConstraintFlag(
                RuleId,
                $"Concurrent QT-prolonging medications: {names} (illustrative check, pending clinical validation).",
                [.. matches.Select(m => m.Source)]),
        ];
    }
}
