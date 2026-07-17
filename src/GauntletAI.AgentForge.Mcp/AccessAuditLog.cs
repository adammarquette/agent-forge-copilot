using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Mcp;

/// <summary>
/// The access-audit trail (FR-AUTH-4): who accessed which patient's data, through which tool, for
/// which correlated request. Deliberately distinct from every other log in this codebase - this is
/// the one place a patient id is expected to appear, because an access-audit trail structurally
/// requires it (the same reason every HIPAA-covered EHR's own audit log carries a patient
/// reference). It is not general application logging and does not relax "no PHI in logs"
/// elsewhere; in production this stream would be routed to dedicated, access-controlled audit
/// storage, distinct from the general application log sink.
/// </summary>
public static partial class AccessAuditLog
{
    /// <summary>
    /// Records one patient-data access — clinician identity, patient, tool, and correlation id — to the
    /// access-audit stream (FR-AUTH-4). This is the one log that intentionally carries a patient reference; do
    /// not route general application logging through it.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "ACCESS AUDIT: clinician={ClinicianIdentity} accessed patient={PatientId} via tool={ToolName} correlation={CorrelationId}")]
    public static partial void RecordAccess(ILogger logger, string clinicianIdentity, string patientId, string toolName, string correlationId);
}
