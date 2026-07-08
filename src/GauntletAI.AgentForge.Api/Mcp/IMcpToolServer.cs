namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// Read-only, narrowly-scoped FHIR tools exposed to the agent orchestrator (Epic 5)
/// (ARCHITECTURE.md §8.1, D2). Every tool takes a bounded, minimum-necessary input (patient id,
/// bounded date window) - none of them support a whole-chart or cross-patient read.
/// </summary>
public interface IMcpToolServer
{
    /// <summary>Demographics + active problems + active meds + allergies, one bounded bundle.</summary>
    Task<PatientSummaryResult> GetPatientSummaryAsync(
        GetPatientSummaryRequest request, CancellationToken cancellationToken);

    /// <summary>Lab Observations (INR, K+, Cr, lipids, BNP) with values, units, dates, reference ranges.</summary>
    Task<LabsResult> GetLabsAsync(GetLabsRequest request, CancellationToken cancellationToken);

    /// <summary>Vital-signs Observations (BP, HR).</summary>
    Task<VitalsResult> GetVitalsAsync(GetVitalsRequest request, CancellationToken cancellationToken);

    /// <summary>A thin list of the most recent encounters (date, type, reason).</summary>
    Task<RecentEncountersResult> GetRecentEncountersAsync(
        GetRecentEncountersRequest request, CancellationToken cancellationToken);

    /// <summary>DiagnosticReport / DocumentReference narrative (echo/EF, device), with source ref.</summary>
    Task<DocumentsResult> GetDocumentsAsync(GetDocumentsRequest request, CancellationToken cancellationToken);
}
