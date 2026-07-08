namespace GauntletAI.AgentForge.Integration.OpenEmr.Http;

/// <summary>
/// Resolves the authenticated clinician's identity for the request currently in flight, so it can
/// be recorded on every patient-data access audit entry (FR-AUTH-4). The BFF (Epic 3) is the
/// concrete implementation: it holds the identity from token introspection, keyed to the browser
/// session, the same way <see cref="IAccessTokenProvider"/> holds the token itself.
/// </summary>
public interface IClinicianIdentityAccessor
{
    /// <summary>The authenticated clinician's identity for the current request, or <see langword="null"/> if none is available.</summary>
    string? ClinicianIdentity { get; }
}
