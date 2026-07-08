using GauntletAI.AgentForge.Integration.OpenEmr.Http;

namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>
/// Settable clinician identity for integration tests - lets a single test simulate a sequence of
/// calls made by different clinicians, the way the audit-trail-reconstruction test needs to.
/// </summary>
internal sealed class MutableClinicianIdentityAccessor : IClinicianIdentityAccessor
{
    public string? ClinicianIdentity { get; set; }
}
