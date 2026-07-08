using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.Verification.Rules;

/// <summary>
/// Flags an INR outside the therapeutic range for the patient's anticoagulation indication
/// (ARCHITECTURE.md §9.2). Illustrative ranges, pending clinical validation before pilot
/// (PRD.md FR-VERIF-2) - not a claim of clinical completeness.
/// </summary>
public sealed class InrTherapeuticRangeRule : IDomainConstraintRule
{
    private const double AfibLow = 2.0;
    private const double AfibHigh = 3.0;
    private const double MechanicalValveLow = 2.5;
    private const double MechanicalValveHigh = 3.5;

    private static readonly string[] MechanicalValveKeywords = ["mechanical valve", "mechanical mitral", "mechanical aortic", "prosthetic valve"];

    /// <inheritdoc />
    public string RuleId => "inr-therapeutic-range";

    /// <inheritdoc />
    public IReadOnlyList<DomainConstraintFlag> Evaluate(DomainConstraintInput input)
    {
        var (low, high, indicationLabel) = DetermineTherapeuticRange(input.ActiveProblems);
        List<DomainConstraintFlag> flags = [];

        foreach (var lab in input.Labs)
        {
            if (lab.Value is not { } value || !lab.CodeDisplay.Contains("INR", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value < low || value > high)
            {
                flags.Add(new DomainConstraintFlag(
                    RuleId,
                    $"INR {value:0.0} is outside the {low:0.0}-{high:0.0} therapeutic range for {indicationLabel} " +
                    "(illustrative range, pending clinical validation).",
                    [lab.Source]));
            }
        }

        return flags;
    }

    private static (double Low, double High, string IndicationLabel) DetermineTherapeuticRange(
        IReadOnlyList<ConditionRecord> activeProblems)
    {
        if (activeProblems.Any(p => MechanicalValveKeywords.Any(
            keyword => p.ProblemDisplay.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
        {
            return (MechanicalValveLow, MechanicalValveHigh, "a mechanical valve");
        }

        // Default to AFib's range when the indication isn't clearly on file - AFib is this
        // product's most common anticoagulation indication (USERS.md §1.1) and a reasonable
        // illustrative default, not a substitute for a clinician confirming the real indication.
        return (AfibLow, AfibHigh, "atrial fibrillation");
    }
}
