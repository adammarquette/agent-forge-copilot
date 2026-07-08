namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>
/// QA OpenEMR connection details for integration tests, bound from environment variables (never
/// hard-coded - tests/AGENTS.md). Distinct from the production
/// GauntletAI.AgentForge.Integration.OpenEmr.OpenEmrOptions: this carries test-only extras
/// (a pre-obtained access token) that production config never has, since production always
/// obtains a token through the interactive SMART launch, not a static value.
/// </summary>
public sealed class QaOpenEmrOptions
{
    /// <summary>Environment variable prefix: OpenEmrQa__BaseUrl, OpenEmrQa__Site, etc.</summary>
    public const string SectionName = "OpenEmrQa";

    /// <summary>Base URL of the QA OpenEMR deployment.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>OpenEMR multi-site segment.</summary>
    public required string Site { get; init; }

    /// <summary>
    /// A valid access token for a test patient, obtained once out-of-band via the interactive
    /// SMART launch flow and stored as a CI/CD variable. Optional - tests that need it fail with
    /// a clear message rather than silently pass when it's absent (NFR-REL-2's "meaningful
    /// check, not an unconditional pass" applied to test config, not just /ready).
    /// </summary>
    public string? TestAccessToken { get; init; }

    /// <summary>The patient id <see cref="TestAccessToken"/> is scoped to, when set.</summary>
    public string? TestPatientId { get; init; }
}
