using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.Api.Agenda;

/// <summary>One patient's row in the Daily Agenda (ARCHITECTURE.md §19).</summary>
/// <param name="PatientId">The scheduled patient's id.</param>
/// <param name="ScheduledStart">When the appointment is scheduled to start.</param>
/// <param name="Summary">The short, cited summary, or <see langword="null"/> when <see cref="Failed"/>.</param>
/// <param name="SafetyFlags">Cardiology domain-constraint flags raised for this patient (FR-VERIF-2).</param>
/// <param name="Failed">
/// True when this patient's summary could not be generated - the rest of the agenda still
/// returns (UC-5: one failure never kills the run).
/// </param>
/// <param name="FailureReason">A safe, generic explanation when <see cref="Failed"/> - never the raw exception text.</param>
public sealed record AgendaRow(
    string PatientId,
    DateTimeOffset ScheduledStart,
    string? Summary,
    IReadOnlyList<DomainConstraintFlag> SafetyFlags,
    bool Failed,
    string? FailureReason);
