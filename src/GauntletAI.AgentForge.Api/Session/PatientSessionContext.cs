namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// The authenticated clinician session's token and launch patient context, held server-side and
/// keyed to the browser session cookie - never sent to the browser itself (ARCHITECTURE.md D11).
/// </summary>
/// <param name="AccessToken">Bearer token obtained from the SMART EHR launch, attached to outbound FHIR calls only.</param>
/// <param name="Site">OpenEMR multi-site segment for this session.</param>
/// <param name="PatientId">The one patient this entire session is scoped to (FR-CHAT-3).</param>
public sealed record PatientSessionContext(string AccessToken, string Site, string PatientId);
