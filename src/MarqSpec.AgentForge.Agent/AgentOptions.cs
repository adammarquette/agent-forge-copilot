using System.ComponentModel.DataAnnotations;

namespace MarqSpec.AgentForge.Agent;

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
    /// config alone. Raised 60 -> 90s (gitlab#125) once the brief began calling get_document_facts /
    /// retrieve_evidence: the extra tools add an LLM round-trip, and a slow-API turn was tripping the
    /// old 60s bound and degrading an otherwise-good brief to the raw-data fallback. Still inside the
    /// ~90s between-rooms window - a safety bound, not the interactive-latency target.
    /// </summary>
    public TimeSpan TurnDeadline { get; init; } = TimeSpan.FromSeconds(90);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TurnDeadline <= TimeSpan.Zero)
        {
            yield return new ValidationResult($"{nameof(TurnDeadline)} must be positive.", [nameof(TurnDeadline)]);
        }
    }
}
