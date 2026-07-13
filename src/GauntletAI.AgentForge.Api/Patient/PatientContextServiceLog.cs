using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Patient;

/// <summary>
/// Source-generated log messages for <see cref="PatientContextService"/> (CA1848). No PHI - the
/// FHIR resource type and patient id only, same as <c>AgendaRosterServiceLog</c>
/// (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class PatientContextServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Patient-context {ResourceType} fetch failed for patient {PatientId}, degrading to unreachable: {Reason}")]
    public static partial void FetchFailed(ILogger logger, string resourceType, string patientId, string reason);
}
