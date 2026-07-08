using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.IntegrationTests.Support;
using GauntletAI.AgentForge.Mcp;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.IntegrationTests.Mcp;

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

        if (string.IsNullOrEmpty(OpenEmr.Options.TestAccessToken) || string.IsNullOrEmpty(OpenEmr.Options.TestPatientId) ||
            string.IsNullOrEmpty(OpenEmr.Options.SecondTestAccessToken) || string.IsNullOrEmpty(OpenEmr.Options.SecondTestPatientId))
        {
            throw new InvalidOperationException(
                $"Cross-identity authorization tests require {QaOpenEmrOptions.SectionName}__TestAccessToken/" +
                "TestPatientId AND SecondTestAccessToken/SecondTestPatientId - two real, distinct SMART EHR " +
                "launches are the only way to prove entitlement is per-identity (ARCHITECTURE.md §5.3/§5.6), " +
                "not one grant standing in for two (tests/AGENTS.md - nothing here is mocked).");
        }

        ToolServerA = new McpToolServer(
            new OpenEmrFhirClient(OpenEmr.FhirApi),
            new FixedCorrelationIdAccessor("qa-cross-identity-test-a"),
            NullLogger<McpToolServer>.Instance);

        ToolServerB = new McpToolServer(
            new OpenEmrFhirClient(OpenEmr.SecondFhirApi),
            new FixedCorrelationIdAccessor("qa-cross-identity-test-b"),
            NullLogger<McpToolServer>.Instance);
    }
}
