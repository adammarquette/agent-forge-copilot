using GauntletAI.AgentForge.Integration.OpenEmr.Http;

namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// The settable half of <see cref="IClinicianIdentityAccessor"/> - a hub method populates
/// <see cref="ClinicianIdentity"/> once from the session at the start of a call, mirroring how
/// <see cref="IScopedAccessTokenProvider"/> populates the token.
/// </summary>
public interface IScopedClinicianIdentityAccessor : IClinicianIdentityAccessor
{
    /// <summary>The identity to hand out for the remainder of this scope, or <see langword="null"/> if unset.</summary>
    new string? ClinicianIdentity { get; set; }
}
