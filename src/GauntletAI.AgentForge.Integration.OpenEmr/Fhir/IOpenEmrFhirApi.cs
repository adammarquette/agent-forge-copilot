using Refit;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// The FHIR R4 Data API (INTERFACE_CONTROL.md Interface B). Every search is patient-scoped -
/// there is no whole-chart or cross-patient read, by design (minimum-necessary,
/// ARCHITECTURE.md §5.4). Methods return the raw FHIR JSON string; the corresponding mapper in
/// this namespace parses it (ENGINEERING_STANDARDS.md §4's IOpenEmrFhirApi example).
/// </summary>
public interface IOpenEmrFhirApi
{
    /// <summary>Direct read of a single resource, e.g. Patient by id (INTERFACE_CONTROL.md §B.2).</summary>
    [Get("/apis/{site}/fhir/{resourceType}/{id}")]
    Task<string> ReadAsync(
        string site, string resourceType, string id, CancellationToken cancellationToken = default);

    /// <summary>Searches MedicationRequest for one patient.</summary>
    [Get("/apis/{site}/fhir/MedicationRequest")]
    Task<string> SearchMedicationRequestsAsync(
        string site, [AliasAs("patient")] string patientId, CancellationToken cancellationToken = default);

    /// <summary>Searches MedicationDispense for one patient (the adherence signal).</summary>
    [Get("/apis/{site}/fhir/MedicationDispense")]
    Task<string> SearchMedicationDispensesAsync(
        string site, [AliasAs("patient")] string patientId, CancellationToken cancellationToken = default);

    /// <summary>Searches Condition (the problem list) for one patient.</summary>
    [Get("/apis/{site}/fhir/Condition")]
    Task<string> SearchConditionsAsync(
        string site, [AliasAs("patient")] string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Observation for one patient. <paramref name="category"/> narrows to
    /// <c>laboratory</c> or <c>vital-signs</c>; <paramref name="dateFilter"/> is a FHIR
    /// date-prefixed value (e.g. <c>ge2026-01-01</c>) for interval-scoped queries. Confirmed
    /// search params in INTERFACE_CONTROL.md §B.2.
    /// </summary>
    [Get("/apis/{site}/fhir/Observation")]
    Task<string> SearchObservationsAsync(
        string site,
        [AliasAs("patient")] string patientId,
        [AliasAs("category")] string? category = null,
        [AliasAs("date")] string? dateFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Searches AllergyIntolerance for one patient.</summary>
    [Get("/apis/{site}/fhir/AllergyIntolerance")]
    Task<string> SearchAllergyIntolerancesAsync(
        string site, [AliasAs("patient")] string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Encounter for one patient - drives the "since last visit" diff.
    /// <paramref name="dateFilter"/> is a FHIR date-prefixed value, when bounding to an interval.
    /// </summary>
    [Get("/apis/{site}/fhir/Encounter")]
    Task<string> SearchEncountersAsync(
        string site,
        [AliasAs("patient")] string patientId,
        [AliasAs("date")] string? dateFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Searches Procedure for one patient.</summary>
    [Get("/apis/{site}/fhir/Procedure")]
    Task<string> SearchProceduresAsync(
        string site, [AliasAs("patient")] string patientId, CancellationToken cancellationToken = default);

    /// <summary>Searches DiagnosticReport (e.g. echo reports) for one patient.</summary>
    [Get("/apis/{site}/fhir/DiagnosticReport")]
    Task<string> SearchDiagnosticReportsAsync(
        string site, [AliasAs("patient")] string patientId, CancellationToken cancellationToken = default);

    /// <summary>Searches DocumentReference (e.g. device interrogations, outside records) for one patient.</summary>
    [Get("/apis/{site}/fhir/DocumentReference")]
    Task<string> SearchDocumentReferencesAsync(
        string site, [AliasAs("patient")] string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads one document's bytes from FHIR <c>Binary</c> — the attachment a <c>DocumentReference</c>
    /// points at (gitlab#109). Patient-scoped: the fork serves this to a patient request with no extra ACL.
    /// Returns the raw <see cref="HttpResponseMessage"/> (not JSON) so the caller reads bytes + media type and
    /// treats a non-success status as "not found".
    /// </summary>
    [Get("/apis/{site}/fhir/Binary/{id}")]
    Task<HttpResponseMessage> GetBinaryAsync(string site, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Appointment by <paramref name="dateFilter"/> only - the deliberate exception to
    /// this interface's "every search is patient-scoped" rule (ARCHITECTURE.md §19). Confirmed
    /// against the fork's <c>FhirAppointmentService::loadSearchParameters()</c>: no
    /// <c>practitioner</c> search parameter exists, so results span every provider's appointments
    /// for the date and must be filtered to one provider by the caller
    /// (<see cref="AppointmentRecord.IsForProvider"/>), not by this query. Pass a single FHIR
    /// equality-precision date value (e.g. <c>eq2026-07-11</c>) to match the whole day - confirmed
    /// against the fork's <c>DateSearchField</c>, which supports fuzzy/implied-precision equality
    /// matching on partial date values.
    /// </summary>
    [Get("/apis/{site}/fhir/Appointment")]
    Task<string> SearchAppointmentsAsync(
        string site, [AliasAs("date")] string dateFilter, CancellationToken cancellationToken = default);
}
