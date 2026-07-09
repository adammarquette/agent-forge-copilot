namespace GauntletAI.AgentForge.Observability;

/// <summary>
/// Application-level metrics feeding the observability dashboard (ARCHITECTURE.md §11,
/// FR-OBS-2/3): agent-turn outcomes/latency, tool-call outcomes/latency, verification pass/fail
/// rate, and LLM token/cost accounting. Wrapped behind an interface - rather than call sites using
/// <see cref="System.Diagnostics.Metrics.Meter"/> directly - so unit tests can fake it like every
/// other external dependency (src/AGENTS.md) instead of needing a live metrics listener.
/// </summary>
public interface IAgentForgeMetrics
{
    /// <summary>Records one completed agent turn (a full brief or follow-up).</summary>
    void RecordAgentTurn(bool succeeded, TimeSpan duration);

    /// <summary>Records one MCP tool call dispatched to the model.</summary>
    void RecordToolCall(string toolName, bool succeeded, TimeSpan duration);

    /// <summary>Records one verification-gate outcome (FR-VERIF-0).</summary>
    void RecordVerificationResult(bool passed);

    /// <summary>Records token/cost accounting for one LLM call.</summary>
    void RecordLlmUsage(int inputTokens, int outputTokens, decimal estimatedCostUsd);
}
