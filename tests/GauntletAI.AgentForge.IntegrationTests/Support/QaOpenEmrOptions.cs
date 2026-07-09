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

    /// <summary>
    /// The client id of a dynamically-registered client that has already been enabled via the
    /// OpenEMR admin UI (Administration → System → API Clients → Enable). Needed for endpoints
    /// that require client authentication - e.g. introspection - since a freshly self-registered
    /// client is left disabled by OpenEMR and cannot authenticate until an admin approves it, so
    /// tests cannot provision one on the fly the way <see cref="DynamicClientRegistrationTests"/>
    /// or <see cref="TestAccessToken"/> otherwise would. Optional - tests that need it fail with a
    /// clear message rather than silently passing when it's absent.
    /// </summary>
    public string? TestClientId { get; init; }

    /// <summary>The client secret paired with <see cref="TestClientId"/> (empty string for a public client).</summary>
    public string? TestClientSecret { get; init; }

    /// <summary>
    /// A second real access token, obtained the same out-of-band way as <see cref="TestAccessToken"/>
    /// but through a distinct SMART EHR launch - a different clinician identity/patient context, not
    /// a second copy of the same one. Needed to prove entitlement is per-identity, not shared
    /// (ARCHITECTURE.md §5.3/§5.6 - "the Co-Pilot cannot exceed the user's own access"). Optional -
    /// the tests that need it fail with a clear message rather than silently skipping when it's absent.
    /// </summary>
    public string? SecondTestAccessToken { get; init; }

    /// <summary>The patient id <see cref="SecondTestAccessToken"/> is scoped to, when set.</summary>
    public string? SecondTestPatientId { get; init; }

    /// <summary>
    /// Client id of an admin-provisioned confidential client for the client_credentials + JWT-bearer
    /// grant (RFC 7523, GitLab issue #22) - the durable replacement for a manually re-minted
    /// <see cref="TestAccessToken"/>. Optional; when unset, <see cref="OpenEmrQaFixture"/> falls back
    /// to <see cref="TestAccessToken"/>.
    /// </summary>
    public string? SystemClientId { get; init; }

    /// <summary>
    /// Filesystem path to the RSA private key PEM used to sign the client assertion. Backed by a
    /// GitLab File-type CI/CD variable, whose value GitLab replaces at runtime with a path to a temp
    /// file holding the pasted PEM - read as a path, never as inline PEM text.
    /// </summary>
    public string? SystemPrivateKeyPath { get; init; }

    /// <summary>
    /// The <c>kid</c> embedded in both the signed assertion's JWT header and the JWKS registered with
    /// the OpenEMR confidential client for <see cref="SystemClientId"/> - must match exactly.
    /// </summary>
    public string? SystemKeyId { get; init; }

    /// <summary>Space-separated system/* scopes requested on the client_credentials mint.</summary>
    public string? SystemScope { get; init; }
}
