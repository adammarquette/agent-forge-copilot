using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// The MCP tool server implementation (ARCHITECTURE.md §8.1, D2) - the governance choke point
/// between the agent orchestrator and OpenEMR data. Every tool: validates its contract first
/// (NFR-CONTRACT-1), fetches only what its bounded input allows, and emits an audit log tagged
/// with the correlation id and result counts - never the patient id or any clinical value
/// (ENGINEERING_STANDARDS.md §7 - no PHI in logs).
/// </summary>
public sealed class McpToolServer(
    IOpenEmrFhirClient fhirClient,
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<McpToolServer> logger) : IMcpToolServer
{
    /// <inheritdoc />
    public async Task<PatientSummaryResult> GetPatientSummaryAsync(
        GetPatientSummaryRequest request, CancellationToken cancellationToken)
    {
        const string toolName = "get_patient_summary";
        McpToolContract.Validate(toolName, request);

        var patientTask = fhirClient.GetPatientAsync(request.Site, request.PatientId, cancellationToken);
        var problemsTask = fhirClient.GetConditionsAsync(request.Site, request.PatientId, cancellationToken);
        var medicationsTask = fhirClient.GetMedicationRequestsAsync(request.Site, request.PatientId, cancellationToken);
        var allergiesTask = fhirClient.GetAllergiesAsync(request.Site, request.PatientId, cancellationToken);

        await Task.WhenAll(patientTask, problemsTask, medicationsTask, allergiesTask).ConfigureAwait(false);

        var result = new PatientSummaryResult(
            await patientTask.ConfigureAwait(false),
            await problemsTask.ConfigureAwait(false),
            await medicationsTask.ConfigureAwait(false),
            await allergiesTask.ConfigureAwait(false));

        McpToolServerLog.PatientSummaryCompleted(
            logger,
            toolName,
            correlationIdAccessor.CorrelationId,
            result.Patient is not null,
            result.ActiveProblems.Count,
            result.ActiveMedications.Count,
            result.Allergies.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<LabsResult> GetLabsAsync(GetLabsRequest request, CancellationToken cancellationToken)
    {
        const string toolName = "get_labs";
        McpToolContract.Validate(toolName, request);

        var labs = await fhirClient.GetObservationsAsync(
            request.Site, request.PatientId, "laboratory", request.SinceDate, cancellationToken)
            .ConfigureAwait(false);

        McpToolServerLog.ResultCountCompleted(logger, toolName, correlationIdAccessor.CorrelationId, labs.Count);

        return new LabsResult(labs);
    }

    /// <inheritdoc />
    public async Task<VitalsResult> GetVitalsAsync(GetVitalsRequest request, CancellationToken cancellationToken)
    {
        const string toolName = "get_vitals";
        McpToolContract.Validate(toolName, request);

        var vitals = await fhirClient.GetObservationsAsync(
            request.Site, request.PatientId, "vital-signs", request.SinceDate, cancellationToken)
            .ConfigureAwait(false);

        McpToolServerLog.ResultCountCompleted(logger, toolName, correlationIdAccessor.CorrelationId, vitals.Count);

        return new VitalsResult(vitals);
    }
}
