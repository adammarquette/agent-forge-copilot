namespace MarqSpec.AgentForge.Api.Session;

/// <summary>
/// The authenticated clinician's provider-wide Daily Agenda session token, held server-side and
/// keyed to the browser session cookie - never sent to the browser itself (ARCHITECTURE.md D11,
/// §19). Deliberately carries no <c>PatientId</c>, mirroring how <see cref="PatientSessionContext"/>
/// deliberately carries exactly one.
/// </summary>
/// <param name="AccessToken">Bearer token from the agenda SMART launch - provider-wide, not scoped to one patient.</param>
/// <param name="Site">OpenEMR multi-site segment for this session.</param>
/// <param name="ClinicianIdentity">
/// The authenticated clinician's subject identifier (from token introspection). Also the FHIR
/// <c>Practitioner.id</c> for this clinician - confirmed identical, both key off <c>users.uuid</c>
/// (ARCHITECTURE.md §19.2) - so no separate lookup is needed to resolve the provider for the
/// roster query.
/// </param>
public sealed record AgendaSessionContext(string AccessToken, string Site, string ClinicianIdentity);
