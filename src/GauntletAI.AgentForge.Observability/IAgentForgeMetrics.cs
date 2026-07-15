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

    // Week 2 (Multimodal Evidence Agent, W2_ARCHITECTURE.md §10 / FR-OBS-W2-1): the supervisor graph and the
    // ingestion path were previously invisible to telemetry. These make document ingestion, per-worker
    // latency, routing, and evidence-retrieval observable without any PHI in the emitted dimensions.

    /// <summary>Records one document-ingestion attempt (pre-visit path), by outcome (ingested/already/rejected).</summary>
    void RecordDocumentIngestion(string outcome, TimeSpan duration);

    /// <summary>Records the wall-clock latency of one Week 2 graph worker (e.g. intake-extractor, evidence-retriever, answer-composer, critic).</summary>
    void RecordWorkerLatency(string worker, TimeSpan duration);

    /// <summary>Records one supervisor routing decision (a logged handoff), by originating and destination node.</summary>
    void RecordRoutingDecision(string fromNode, string toNode);

    /// <summary>Records one evidence-retrieval call: whether it hit (returned any snippets), how many, and how long it took.</summary>
    void RecordEvidenceRetrieval(bool hit, int resultCount, TimeSpan duration);
}
