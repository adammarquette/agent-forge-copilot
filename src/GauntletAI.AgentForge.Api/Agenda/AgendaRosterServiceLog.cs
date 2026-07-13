using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Agenda;

/// <summary>
/// Source-generated log messages for <see cref="AgendaRosterService"/> (CA1848). No PHI - patient
/// id only (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class AgendaRosterServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Agenda summary failed for patient {PatientId}: {Reason}")]
    public static partial void PatientSummaryFailed(ILogger logger, string patientId, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Agenda patient name unavailable for {PatientId}, falling back to id: {Reason}")]
    public static partial void PatientNameUnavailable(ILogger logger, string patientId, string reason);
}
