using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Agenda;

/// <summary>
/// Source-generated log messages for <see cref="AgendaEndpoints"/> (CA1848). No PHI beyond the
/// patient/clinician identifiers audit logging already carries elsewhere in this codebase
/// (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class AgendaEndpointsLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Clinician {ClinicianIdentity} attempted to select patient {PatientId}, which was not part of their own agenda roster - rejected (FR-AUTH-3)")]
    public static partial void PatientNotInRoster(ILogger logger, string clinicianIdentity, string patientId);
}
