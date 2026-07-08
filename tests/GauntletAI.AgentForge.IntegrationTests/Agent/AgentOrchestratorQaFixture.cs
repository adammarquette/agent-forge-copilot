using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.IntegrationTests.Llm;
using GauntletAI.AgentForge.IntegrationTests.Support;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Observability;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.IntegrationTests.Agent;

/// <summary>
/// Shared fixture wiring the real <see cref="AgentOrchestrator"/> end to end - the real
/// <see cref="AnthropicQaFixture"/> LLM provider, a real <see cref="McpToolDispatcher"/> in
/// front of the real QA OpenEMR deployment via <see cref="McpToolServerQaFixture"/>'s wiring, and
/// the real <see cref="ClinicalResponseVerifier"/> (Epic 7) with the default rule set - no fake
/// standing in for the mandatory verification gate here either. Nothing here is mocked
/// (tests/AGENTS.md): this is the only tier that proves the full loop - model plans a tool call,
/// the real dispatcher executes it against real FHIR data, the result serializes back onto the
/// real Anthropic wire format, the model produces a final answer, and that answer passes through
/// real source attribution before this fixture ever sees it.
/// </summary>
public sealed class AgentOrchestratorQaFixture
{
    /// <summary>The underlying OpenEMR QA connection (base URL, site, test patient id).</summary>
    public OpenEmrQaFixture OpenEmr { get; }

    /// <summary>The real orchestrator, ready to run a turn.</summary>
    public AgentOrchestrator Orchestrator { get; }

    public AgentOrchestratorQaFixture()
    {
        var llm = new AnthropicQaFixture();
        OpenEmr = new OpenEmrQaFixture();

        if (string.IsNullOrEmpty(OpenEmr.Options.TestAccessToken) || string.IsNullOrEmpty(OpenEmr.Options.TestPatientId))
        {
            throw new InvalidOperationException(
                $"Agent orchestrator integration tests require {QaOpenEmrOptions.SectionName}__TestAccessToken " +
                $"and {QaOpenEmrOptions.SectionName}__TestPatientId - the full loop needs an authenticated " +
                "session against a real test patient for every tool call the model makes " +
                "(tests/AGENTS.md - nothing here is mocked).");
        }

        var fhirClient = new OpenEmrFhirClient(OpenEmr.FhirApi);
        var toolServer = new McpToolServer(
            fhirClient,
            new FixedCorrelationIdAccessor("qa-agent-orchestrator-test"),
            NullLogger<McpToolServer>.Instance);
        var metrics = new AgentForgeMetrics();
        var dispatcher = new McpToolDispatcher(toolServer, metrics, NullLogger<McpToolDispatcher>.Instance);
        var verifier = new ClinicalResponseVerifier(
            new SourceAttributionEngine(),
            new CardiologyConstraintEngine(CardiologyConstraintRules.Default),
            NullLogger<ClinicalResponseVerifier>.Instance);

        Orchestrator = new AgentOrchestrator(llm.Provider, dispatcher, verifier, metrics, NullLogger<AgentOrchestrator>.Instance);
    }
}
