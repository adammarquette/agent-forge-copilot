namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// Registered per-request/per-hub-invocation scope (never singleton): holds the current session's
/// clinician identity for the lifetime of one call, so the MCP audit log (FR-AUTH-4) can record
/// who made each access without threading an extra parameter through every layer between the BFF
/// and the tool server - the same reasoning as <see cref="ScopedAccessTokenProvider"/>.
/// </summary>
public sealed class ScopedClinicianIdentityAccessor : IScopedClinicianIdentityAccessor
{
    /// <inheritdoc />
    public string? ClinicianIdentity { get; set; }
}
