namespace GauntletAI.AgentForge.Llm;

/// <summary>
/// Seam over the model tier (ARCHITECTURE.md §12, D12). One implementation in v1; alternates
/// (direct API, air-gapped local) are described as seams, not built. Inference only - the
/// orchestrator (Epic 5) owns the multi-turn loop and tool-chaining decisions.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Sends one request and returns the model's response.</summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}
