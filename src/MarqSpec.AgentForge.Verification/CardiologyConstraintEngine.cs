using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.Verification;

/// <summary>
/// Runs every configured <see cref="IDomainConstraintRule"/> against one turn's data
/// (ARCHITECTURE.md §9.2, FR-VERIF-2). A rule that throws degrades that one check, not the whole
/// verification pass (NFR-REL-1) - the response still ships with whatever the other rules found.
/// </summary>
public sealed class CardiologyConstraintEngine(
    IReadOnlyList<IDomainConstraintRule> rules, ILogger<CardiologyConstraintEngine> logger)
{
    /// <summary>Convenience constructor for the default rule set with no logging (e.g. simple callers/tests).</summary>
    public CardiologyConstraintEngine(IReadOnlyList<IDomainConstraintRule> rules)
        : this(rules, Microsoft.Extensions.Logging.Abstractions.NullLogger<CardiologyConstraintEngine>.Instance)
    {
    }

    /// <summary>Evaluates <paramref name="input"/> against every configured rule and returns every flag raised.</summary>
    public IReadOnlyList<DomainConstraintFlag> Evaluate(DomainConstraintInput input)
    {
        List<DomainConstraintFlag> flags = [];

        foreach (var rule in rules)
        {
            try
            {
                flags.AddRange(rule.Evaluate(input));
            }
            catch (Exception ex)
            {
                CardiologyConstraintEngineLog.RuleThrew(logger, rule.RuleId, ex);
            }
        }

        return flags;
    }
}
