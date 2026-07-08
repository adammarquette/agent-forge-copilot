using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// The structured clinical data a <see cref="IDomainConstraintRule"/> evaluates - read directly
/// from tool results, never from the model's prose (ARCHITECTURE.md §9.2: rules evaluate values,
/// not model-internal knowledge or re-parsed free text).
/// </summary>
/// <param name="ActiveMedications">Every active medication returned by a tool this turn.</param>
/// <param name="Labs">Every lab/vital Observation returned by a tool this turn.</param>
/// <param name="ActiveProblems">Every active problem returned by a tool this turn (drives indication detection).</param>
public sealed record DomainConstraintInput(
    IReadOnlyList<MedicationRecord> ActiveMedications,
    IReadOnlyList<ObservationRecord> Labs,
    IReadOnlyList<ConditionRecord> ActiveProblems)
{
    /// <summary>An input with nothing in it, for a turn where no relevant tool was called.</summary>
    public static DomainConstraintInput Empty { get; } = new([], [], []);
}
