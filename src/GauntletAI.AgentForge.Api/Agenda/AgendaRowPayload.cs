using GauntletAI.AgentForge.Api.Chat;

namespace GauntletAI.AgentForge.Api.Agenda;

/// <summary>Wire shape for one <see cref="AgendaRow"/>.</summary>
/// <param name="PatientId">The scheduled patient's id.</param>
/// <param name="ScheduledStart">When the appointment is scheduled to start.</param>
/// <param name="Summary">The short, cited summary, or <see langword="null"/> when <see cref="Failed"/>.</param>
/// <param name="SafetyFlags">Cardiology domain-constraint flags raised for this patient (FR-VERIF-2).</param>
/// <param name="Failed">True when this patient's summary could not be generated.</param>
/// <param name="FailureReason">A safe, generic explanation when <see cref="Failed"/>.</param>
public sealed record AgendaRowPayload(
    string PatientId,
    DateTimeOffset ScheduledStart,
    string? Summary,
    IReadOnlyList<SafetyFlagPayload> SafetyFlags,
    bool Failed,
    string? FailureReason);
