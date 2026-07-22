using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.IntegrationTests.Support;
using MarqSpec.AgentForge.Mcp;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarqSpec.AgentForge.IntegrationTests.Mcp;

/// <summary>
/// Wires two real <see cref="McpToolServer"/> instances against the same QA OpenEMR deployment,
/// each authenticated as a distinct clinician identity from a distinct real SMART EHR launch
/// (<see cref="OpenEmrQaFixture.FhirApi"/> / <see cref="OpenEmrQaFixture.SecondFhirApi"/>). Proving
/// entitlement is per-identity - not shared, not a single blanket grant - needs two real tokens;
/// nothing here is mocked (tests/AGENTS.md).
/// </summary>
public sealed class CrossIdentityAuthorizationQaFixture
{
    /// <summary>The underlying OpenEMR QA connection, carrying both identities' tokens.</summary>
    public OpenEmrQaFixture OpenEmr { get; }

    /// <summary>The first clinician identity's patient id (their own SMART launch context).</summary>
    public string PatientIdA => OpenEmr.Options.TestPatientId!;

    /// <summary>The second clinician identity's patient id (their own, distinct SMART launch context).</summary>
    public string PatientIdB => OpenEmr.Options.SecondTestPatientId!;

    /// <summary>The real tool server authenticated as identity A.</summary>
    public IMcpToolServer ToolServerA { get; }

    /// <summary>The real tool server authenticated as identity B.</summary>
    public IMcpToolServer ToolServerB { get; }

    public CrossIdentityAuthorizationQaFixture()
    {
        OpenEmr = new OpenEmrQaFixture();

        if (string.IsNullOrEmpty(OpenEmr.Options.CrossIdentityTestAccessTokenA) || string.IsNullOrEmpty(OpenEmr.Options.TestPatientId) ||
            string.IsNullOrEmpty(OpenEmr.Options.SecondTestAccessToken) || string.IsNullOrEmpty(OpenEmr.Options.SecondTestPatientId))
        {
            throw new InvalidOperationException(
                $"Cross-identity authorization tests require {QaOpenEmrOptions.SectionName}__CrossIdentityTestAccessTokenA/" +
                "TestPatientId AND SecondTestAccessToken/SecondTestPatientId - two real, distinct SMART EHR " +
                "launches are the only way to prove entitlement is per-identity (ARCHITECTURE.md §5.3/§5.6), " +
                "not one grant standing in for two (tests/AGENTS.md - nothing here is mocked). Note this is " +
                "CrossIdentityTestAccessTokenA, not the shared TestAccessToken: once SystemClientId is " +
                "configured (issue #22), TestAccessToken is a system-role client_credentials grant that can " +
                "read every patient, which would make this class's isolation checks meaningless (issue #27).");
        }

        ToolServerA = new McpToolServer(
            new OpenEmrFhirClient(OpenEmr.CrossIdentityFhirApiA),
            new FixedCorrelationIdAccessor("qa-cross-identity-test-a"),
            NullLogger<McpToolServer>.Instance);

        ToolServerB = new McpToolServer(
            new OpenEmrFhirClient(OpenEmr.SecondFhirApi),
            new FixedCorrelationIdAccessor("qa-cross-identity-test-b"),
            NullLogger<McpToolServer>.Instance);
    }
}
