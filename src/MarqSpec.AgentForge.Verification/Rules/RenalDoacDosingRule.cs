namespace MarqSpec.AgentForge.Verification.Rules;

/// <summary>
/// Flags a DOAC alongside meaningfully elevated creatinine (ARCHITECTURE.md §9.2) - DOAC dosing is
/// renally cleared and often needs adjustment as renal function declines; this is a prompt to
/// verify the dose against creatinine clearance, not a computed CrCl (Cockcroft-Gault needs
/// weight/age/sex this layer doesn't have). Illustrative drug list and threshold, pending clinical
/// validation before pilot (PRD.md FR-VERIF-2).
/// </summary>
public sealed class RenalDoacDosingRule : IDomainConstraintRule
{
    /// <summary>Illustrative "meaningfully elevated" creatinine threshold in mg/dL.</summary>
    private const double ElevatedCreatinineThreshold = 1.5;

    /// <summary>DOACs only - warfarin is excluded; it isn't renally dosed the same way.</summary>
    private static readonly string[] Doacs = ["apixaban", "rivaroxaban", "dabigatran", "edoxaban"];

    /// <inheritdoc />
    public string RuleId => "renal-doac-dosing";

    /// <inheritdoc />
    public IReadOnlyList<DomainConstraintFlag> Evaluate(DomainConstraintInput input)
    {
        var doac = input.ActiveMedications
            .Where(m => string.Equals(m.Status, "active", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(m => Doacs.Any(drug => m.MedicationDisplay.Contains(drug, StringComparison.OrdinalIgnoreCase)));

        if (doac is null)
        {
            return [];
        }

        var elevatedCreatinine = input.Labs.FirstOrDefault(
            l => l.CodeDisplay.Contains("creatinine", StringComparison.OrdinalIgnoreCase) &&
                l.Value is { } value && value > ElevatedCreatinineThreshold);

        if (elevatedCreatinine is null)
        {
            return [];
        }

        return
        [
            new DomainConstraintFlag(
                RuleId,
                $"{doac.MedicationDisplay} (DOAC) with creatinine {elevatedCreatinine.Value:0.0} mg/dL - " +
                "verify dose against renal function (illustrative check, pending clinical validation).",
                [doac.Source, elevatedCreatinine.Source]),
        ];
    }
}
