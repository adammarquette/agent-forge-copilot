using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.IntegrationTests.Support;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.IntegrationTests.Verification;

/// <summary>
/// Shared fixture wiring the real <see cref="ISourceAttributionEngine"/> and
/// <see cref="CardiologyConstraintEngine"/> against real tool results from the same QA OpenEMR
/// deployment Epic 2/4's suites target. Nothing here is mocked (tests/AGENTS.md): the value this
/// tier adds over the unit suite is proving the engines work against the real JSON
/// <see cref="McpToolServer"/> actually emits and real FHIR field text, not hand-built fixtures.
/// </summary>
public sealed class VerificationQaFixture
{
    /// <summary>The underlying OpenEMR QA connection (base URL, site, test patient id).</summary>
    public OpenEmrQaFixture OpenEmr { get; }

    /// <summary>The real tool server, ready to call.</summary>
    public IMcpToolServer ToolServer { get; }

    /// <summary>The real source-attribution engine.</summary>
    public ISourceAttributionEngine AttributionEngine { get; } = new SourceAttributionEngine();

    /// <summary>The real domain-constraint engine, with the default illustrative rule set.</summary>
    public CardiologyConstraintEngine ConstraintEngine { get; } = new(CardiologyConstraintRules.Default);

    public VerificationQaFixture()
    {
        OpenEmr = new OpenEmrQaFixture();

        if (string.IsNullOrEmpty(OpenEmr.Options.TestAccessToken) || string.IsNullOrEmpty(OpenEmr.Options.TestPatientId))
        {
            throw new InvalidOperationException(
                $"Verification integration tests require {QaOpenEmrOptions.SectionName}__TestAccessToken and " +
                $"{QaOpenEmrOptions.SectionName}__TestPatientId - proving the engines work against real tool " +
                "results needs a real authenticated session (tests/AGENTS.md - nothing here is mocked).");
        }

        var fhirClient = new OpenEmrFhirClient(OpenEmr.FhirApi);
        ToolServer = new McpToolServer(
            fhirClient, new FixedCorrelationIdAccessor("qa-verification-test"), NullLogger<McpToolServer>.Instance);
    }
}
