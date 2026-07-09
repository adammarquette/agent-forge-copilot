using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.IntegrationTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.IntegrationTests.Mcp;

/// <summary>
/// Shared fixture wiring the real <see cref="McpToolServer"/> - real
/// <see cref="OpenEmrFhirClient"/>, real authenticated FHIR calls - against the same QA OpenEMR
/// deployment Epic 2's suite targets (<see cref="OpenEmrQaFixture"/>). Unlike that fixture,
/// <see cref="QaOpenEmrOptions.TestAccessToken"/> is not optional here: every MCP tool needs an
/// authenticated session to return anything, so there is no unauthenticated variant to fall back
/// to (that boundary is already covered by Epic 2's FhirAuthorizationBoundaryTests).
/// </summary>
public sealed class McpToolServerQaFixture
{
    /// <summary>The underlying OpenEMR QA connection (base URL, site, test patient id).</summary>
    public OpenEmrQaFixture OpenEmr { get; }

    /// <summary>The real tool server, ready to call.</summary>
    public IMcpToolServer ToolServer { get; }

    public McpToolServerQaFixture()
    {
        OpenEmr = new OpenEmrQaFixture();

        if (string.IsNullOrEmpty(OpenEmr.Options.TestAccessToken) || string.IsNullOrEmpty(OpenEmr.Options.TestPatientId))
        {
            throw new InvalidOperationException(
                $"MCP tool integration tests require {QaOpenEmrOptions.SectionName}__TestAccessToken and " +
                $"{QaOpenEmrOptions.SectionName}__TestPatientId - every tool needs an authenticated session " +
                "against a real test patient to return anything (tests/AGENTS.md - nothing here is mocked).");
        }

        var fhirClient = new OpenEmrFhirClient(OpenEmr.FhirApi);
        ToolServer = new McpToolServer(
            fhirClient,
            new FixedCorrelationIdAccessor("qa-mcp-test"),
            NullLogger<McpToolServer>.Instance);
    }
}
