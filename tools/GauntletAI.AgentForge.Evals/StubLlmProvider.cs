using GauntletAI.AgentForge.Llm;

namespace GauntletAI.AgentForge.Evals;

/// <summary>Returns a fixed, canned response so the eval gate is deterministic and needs no live API.</summary>
internal sealed class StubLlmProvider(string cannedResponse) : ILlmProvider
{
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new LlmResponse(cannedResponse, [], LlmStopReason.EndTurn, new LlmUsage(0, 0, 0m)));
}
