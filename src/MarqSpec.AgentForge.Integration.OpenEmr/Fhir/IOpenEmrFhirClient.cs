namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Typed, cited access to one patient's FHIR data. Extracted as an interface so consumers (the
/// MCP tool layer, Epic 4) can be unit-tested against a fake rather than the real HTTP-backed
/// <see cref="OpenEmrFhirClient"/>.
/// </summary>
public interface IOpenEmrFhirClient
{
    /// <summary>Fetches one patient's demographics, or null if not found.</summary>
    Task<PatientRecord?> GetPatientAsync(string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's active/recent medication requests.</summary>
    Task<IReadOnlyList<MedicationRecord>> GetMedicationRequestsAsync(
        string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's medication dispense (fill/adherence) history.</summary>
    Task<IReadOnlyList<MedicationDispenseRecord>> GetMedicationDispensesAsync(
        string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's problem list.</summary>
    Task<IReadOnlyList<ConditionRecord>> GetConditionsAsync(
        string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's lab/vital observations, optionally scoped by category and/or since-date.</summary>
    Task<IReadOnlyList<ObservationRecord>> GetObservationsAsync(
        string site, string patientId, string? category, string? dateFilter, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's allergies/intolerances.</summary>
    Task<IReadOnlyList<AllergyRecord>> GetAllergiesAsync(
        string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's encounters, optionally since a given date (the interval-change diff).</summary>
    Task<IReadOnlyList<EncounterRecord>> GetEncountersAsync(
        string site, string patientId, string? dateFilter, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's procedures.</summary>
    Task<IReadOnlyList<ProcedureRecord>> GetProceduresAsync(
        string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's diagnostic reports (e.g. echo).</summary>
    Task<IReadOnlyList<ClinicalDocumentRecord>> GetDiagnosticReportsAsync(
        string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Fetches one patient's document references (e.g. device interrogations, outside records).</summary>
    Task<IReadOnlyList<ClinicalDocumentRecord>> GetDocumentReferencesAsync(
        string site, string patientId, CancellationToken cancellationToken);

    /// <summary>Downloads one document's bytes + media type from FHIR <c>Binary</c>; null if not found (gitlab#109).</summary>
    Task<BinaryDocument?> GetBinaryAsync(string site, string documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches appointments for <paramref name="dateFilter"/> across every provider - the
    /// deliberate exception to this client's usual per-patient scoping (ARCHITECTURE.md §19). The
    /// caller is responsible for filtering to one provider via
    /// <see cref="AppointmentRecord.IsForProvider"/>.
    /// </summary>
    Task<IReadOnlyList<AppointmentRecord>> GetAppointmentsAsync(
        string site, string dateFilter, CancellationToken cancellationToken);
}
