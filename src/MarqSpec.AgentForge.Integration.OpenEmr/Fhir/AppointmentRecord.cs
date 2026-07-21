namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// One scheduled appointment, mapped from a FHIR <c>Appointment</c> (INTERFACE_CONTROL.md §B.1,
/// ARCHITECTURE.md §19). Unlike every other record in this namespace, this one is not
/// server-side scoped to a single patient or provider - the fork's <c>Appointment</c> search
/// supports no <c>practitioner</c> parameter, so <see cref="ProviderActorReference"/> is carried
/// through for the caller to filter by provider itself (<see cref="IsForProvider"/>).
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="PatientId">The scheduled patient's id, when a <c>Patient</c> participant is present.</param>
/// <param name="ProviderActorReference">
/// The provider participant's raw actor reference, e.g. <c>Practitioner/{uuid}</c> or
/// <c>Person/{uuid}</c> - the fork only emits <c>Practitioner/</c> when the provider has an NPI on
/// file, <c>Person/</c> otherwise (confirmed against <c>FhirAppointmentService::parseOpenEMRRecord</c>).
/// </param>
/// <param name="Status">FHIR <c>Appointment.status</c> (booked, pending, cancelled, ...).</param>
/// <param name="ScheduledStart">When the appointment is scheduled to start, when present.</param>
public sealed record AppointmentRecord(
    ClinicalSourceRef Source,
    string? PatientId,
    string? ProviderActorReference,
    string Status,
    DateTimeOffset? ScheduledStart)
{
    /// <summary>
    /// Whether this appointment's provider participant is <paramref name="clinicianIdentity"/> -
    /// matching either the <c>Practitioner/</c> or <c>Person/</c> actor form, since which one the
    /// fork emits depends on an NPI being on file, not on which clinician it is.
    /// </summary>
    public bool IsForProvider(string clinicianIdentity) =>
        ProviderActorReference == $"Practitioner/{clinicianIdentity}" ||
        ProviderActorReference == $"Person/{clinicianIdentity}";
}
