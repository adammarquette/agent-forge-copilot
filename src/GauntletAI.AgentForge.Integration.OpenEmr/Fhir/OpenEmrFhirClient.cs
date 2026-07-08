namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Ties <see cref="IOpenEmrFhirApi"/> to the per-resource mappers in this namespace, so callers
/// (the MCP tool layer, Epic 4) get typed, cited records directly rather than raw FHIR JSON.
/// </summary>
public sealed class OpenEmrFhirClient(IOpenEmrFhirApi api) : IOpenEmrFhirClient
{
    /// <summary>Fetches one patient's demographics, or null if not found.</summary>
    public async Task<PatientRecord?> GetPatientAsync(string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.ReadAsync(site, "Patient", patientId, cancellationToken).ConfigureAwait(false);
        return PatientMapper.MapResource(json);
    }

    /// <summary>Fetches one patient's active/recent medication requests.</summary>
    public async Task<IReadOnlyList<MedicationRecord>> GetMedicationRequestsAsync(
        string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.SearchMedicationRequestsAsync(site, patientId, cancellationToken).ConfigureAwait(false);
        return MedicationRequestMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's medication dispense (fill/adherence) history.</summary>
    public async Task<IReadOnlyList<MedicationDispenseRecord>> GetMedicationDispensesAsync(
        string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.SearchMedicationDispensesAsync(site, patientId, cancellationToken).ConfigureAwait(false);
        return MedicationDispenseMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's problem list.</summary>
    public async Task<IReadOnlyList<ConditionRecord>> GetConditionsAsync(
        string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.SearchConditionsAsync(site, patientId, cancellationToken).ConfigureAwait(false);
        return ConditionMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's lab/vital observations, optionally scoped by category and/or since-date.</summary>
    public async Task<IReadOnlyList<ObservationRecord>> GetObservationsAsync(
        string site, string patientId, string? category, string? dateFilter, CancellationToken cancellationToken)
    {
        var json = await api.SearchObservationsAsync(site, patientId, category, dateFilter, cancellationToken).ConfigureAwait(false);
        return ObservationMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's allergies/intolerances.</summary>
    public async Task<IReadOnlyList<AllergyRecord>> GetAllergiesAsync(
        string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.SearchAllergyIntolerancesAsync(site, patientId, cancellationToken).ConfigureAwait(false);
        return AllergyIntoleranceMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's encounters, optionally since a given date (the interval-change diff).</summary>
    public async Task<IReadOnlyList<EncounterRecord>> GetEncountersAsync(
        string site, string patientId, string? dateFilter, CancellationToken cancellationToken)
    {
        var json = await api.SearchEncountersAsync(site, patientId, dateFilter, cancellationToken).ConfigureAwait(false);
        return EncounterMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's procedures.</summary>
    public async Task<IReadOnlyList<ProcedureRecord>> GetProceduresAsync(
        string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.SearchProceduresAsync(site, patientId, cancellationToken).ConfigureAwait(false);
        return ProcedureMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's diagnostic reports (e.g. echo).</summary>
    public async Task<IReadOnlyList<ClinicalDocumentRecord>> GetDiagnosticReportsAsync(
        string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.SearchDiagnosticReportsAsync(site, patientId, cancellationToken).ConfigureAwait(false);
        return DiagnosticReportMapper.MapBundle(json);
    }

    /// <summary>Fetches one patient's document references (e.g. device interrogations, outside records).</summary>
    public async Task<IReadOnlyList<ClinicalDocumentRecord>> GetDocumentReferencesAsync(
        string site, string patientId, CancellationToken cancellationToken)
    {
        var json = await api.SearchDocumentReferencesAsync(site, patientId, cancellationToken).ConfigureAwait(false);
        return DocumentReferenceMapper.MapBundle(json);
    }
}
