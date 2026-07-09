using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// Agent orchestration configuration, bound via the Options pattern (ENGINEERING_STANDARDS.md §6).
/// </summary>
public sealed class AgentOptions : IValidatableObject
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "Agent";

    /// <summary>
    /// Bounds a single turn's total wall-clock time across every LLM call and tool-dispatch round
    /// (PRD.md §13.1's "per-request deadline" design default - the "tool slow / hits deadline"
    /// row). Exceeding it degrades to the deterministic fallback (whatever tool data already
    /// succeeded) rather than blocking indefinitely. Placeholder default: NFR-PERF-1 leaves the
    /// real interactive-latency target TBD pending Epic 12's load-test baselining - this exists so
    /// the enforcement mechanism is in place and fault-injection-testable now, tunable later via
    /// config alone.
    /// </summary>
    public TimeSpan TurnDeadline { get; init; } = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TurnDeadline <= TimeSpan.Zero)
        {
            yield return new ValidationResult($"{nameof(TurnDeadline)} must be positive.", [nameof(TurnDeadline)]);
        }
    }
}
