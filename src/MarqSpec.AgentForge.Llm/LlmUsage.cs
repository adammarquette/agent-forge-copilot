namespace MarqSpec.AgentForge.Llm;

/// <summary>Token/cost accounting for one model call (FR-OBS-2 - tokens consumed + cost per request).</summary>
/// <param name="InputTokens">Tokens consumed by the prompt (system + history + tool schemas).</param>
/// <param name="OutputTokens">Tokens the model generated.</param>
/// <param name="EstimatedCostUsd">Cost estimate at the provider's published per-token pricing.</param>
public sealed record LlmUsage(int InputTokens, int OutputTokens, decimal EstimatedCostUsd);
