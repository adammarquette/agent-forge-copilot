namespace GauntletAI.AgentForge.Integration.OpenEmr.Http;

/// <summary>
/// No access token was available for an outbound OpenEMR call (FR-AUTH-1: "an unauthenticated
/// request is rejected before any tool runs"). Distinct from a downstream 401/403 - this call
/// never left the process at all.
/// </summary>
public sealed class UnauthenticatedRequestException : Exception
{
    /// <summary>Creates a new <see cref="UnauthenticatedRequestException"/>.</summary>
    public UnauthenticatedRequestException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
