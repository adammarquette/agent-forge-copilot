namespace GauntletAI.AgentForge.Agents;

/// <summary>
/// The Week 2 multi-agent supervisor (W2_ARCHITECTURE.md §6). Routes a request through the intake-extractor
/// and evidence-retriever workers, composes a grounded answer, and gates it through the critic (the Week 1
/// verification layer, reused). Every handoff is explicit and logged.
/// </summary>
public interface IEvidenceAgentSupervisor
{
    /// <summary>Runs one evidence-agent turn end to end.</summary>
    Task<EvidenceAgentResult> RunAsync(EvidenceAgentRequest request, CancellationToken cancellationToken);
}
