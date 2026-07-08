using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Mcp;

/// <summary>
/// Decorates a real <see cref="IMcpToolServer"/> with access-audit logging (FR-AUTH-4): every tool
/// call records who (clinician identity), what (tool + patient), and the correlation id tying it
/// to the rest of the request, before delegating to the inner server - the "single audit choke
/// point" ARCHITECTURE.md assigns to the MCP layer, in addition to OpenEMR's own EventAuditLogger.
/// Recorded before delegating so a failed or slow inner call is still an audited access attempt,
/// not a silently-missing one.
/// </summary>
public sealed class AuditingMcpToolServer(
    IMcpToolServer inner,
    IClinicianIdentityAccessor clinicianIdentityAccessor,
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<AuditingMcpToolServer> logger) : IMcpToolServer
{
    /// <inheritdoc />
    public Task<PatientSummaryResult> GetPatientSummaryAsync(GetPatientSummaryRequest request, CancellationToken cancellationToken)
    {
        RecordAccess(request.PatientId, "get_patient_summary");
        return inner.GetPatientSummaryAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LabsResult> GetLabsAsync(GetLabsRequest request, CancellationToken cancellationToken)
    {
        RecordAccess(request.PatientId, "get_labs");
        return inner.GetLabsAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VitalsResult> GetVitalsAsync(GetVitalsRequest request, CancellationToken cancellationToken)
    {
        RecordAccess(request.PatientId, "get_vitals");
        return inner.GetVitalsAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RecentEncountersResult> GetRecentEncountersAsync(GetRecentEncountersRequest request, CancellationToken cancellationToken)
    {
        RecordAccess(request.PatientId, "get_recent_encounters");
        return inner.GetRecentEncountersAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<DocumentsResult> GetDocumentsAsync(GetDocumentsRequest request, CancellationToken cancellationToken)
    {
        RecordAccess(request.PatientId, "get_documents");
        return inner.GetDocumentsAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IntervalChangesResult> GetIntervalChangesAsync(GetIntervalChangesRequest request, CancellationToken cancellationToken)
    {
        RecordAccess(request.PatientId, "get_interval_changes");
        return inner.GetIntervalChangesAsync(request, cancellationToken);
    }

    private void RecordAccess(string patientId, string toolName) =>
        AccessAuditLog.RecordAccess(
            logger, clinicianIdentityAccessor.ClinicianIdentity ?? "unknown", patientId, toolName, correlationIdAccessor.CorrelationId);
}
