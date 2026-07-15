using System.Diagnostics.Metrics;

namespace GauntletAI.AgentForge.Observability;

/// <inheritdoc cref="IAgentForgeMetrics" />
public sealed class AgentForgeMetrics : IAgentForgeMetrics, IDisposable
{
    /// <summary>Meter name every OTel <c>MeterProvider</c> registration must include (Program.cs).</summary>
    public const string MeterName = "GauntletAI.AgentForge";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _agentTurnsTotal;
    private readonly Histogram<double> _agentTurnDurationSeconds;
    private readonly Counter<long> _toolCallsTotal;
    private readonly Histogram<double> _toolCallDurationSeconds;
    private readonly Counter<long> _verificationResultsTotal;
    private readonly Counter<long> _llmTokensTotal;
    private readonly Counter<double> _llmCostUsdTotal;
    private readonly Counter<long> _documentIngestionsTotal;
    private readonly Histogram<double> _documentIngestionDurationSeconds;
    private readonly Histogram<double> _workerDurationSeconds;
    private readonly Counter<long> _routingDecisionsTotal;
    private readonly Counter<long> _evidenceRetrievalsTotal;
    private readonly Histogram<double> _evidenceRetrievalDurationSeconds;
    private readonly Histogram<long> _evidenceRetrievalResults;
    private readonly Histogram<double> _rerankDurationSeconds;
    private readonly Counter<long> _retrievalDegradationsTotal;

    /// <summary>Creates the meter and every instrument it publishes under <see cref="MeterName"/>.</summary>
    public AgentForgeMetrics()
    {
        _agentTurnsTotal = _meter.CreateCounter<long>(
            "agentforge.agent_turns", unit: "{turn}", description: "Completed agent turns, by outcome.");
        _agentTurnDurationSeconds = _meter.CreateHistogram<double>(
            "agentforge.agent_turn.duration", unit: "s", description: "Wall-clock duration of one agent turn.");
        _toolCallsTotal = _meter.CreateCounter<long>(
            "agentforge.tool_calls", unit: "{call}", description: "MCP tool calls dispatched, by tool and outcome.");
        _toolCallDurationSeconds = _meter.CreateHistogram<double>(
            "agentforge.tool_call.duration", unit: "s", description: "Wall-clock duration of one MCP tool call.");
        _verificationResultsTotal = _meter.CreateCounter<long>(
            "agentforge.verification_results", unit: "{result}", description: "Verification-gate outcomes, by pass/fail.");
        _llmTokensTotal = _meter.CreateCounter<long>(
            "agentforge.llm_tokens", unit: "{token}", description: "LLM tokens consumed, by direction.");
        _llmCostUsdTotal = _meter.CreateCounter<double>(
            "agentforge.llm_cost_usd", unit: "{USD}", description: "Estimated LLM cost.");

        // Week 2 instruments (FR-OBS-W2-1). Dimensions are bounded, low-cardinality, and PHI-free
        // (outcome/worker/node tags only - never a patient value or document text).
        _documentIngestionsTotal = _meter.CreateCounter<long>(
            "agentforge.document_ingestions", unit: "{document}", description: "Document-ingestion attempts, by outcome.");
        _documentIngestionDurationSeconds = _meter.CreateHistogram<double>(
            "agentforge.document_ingestion.duration", unit: "s", description: "Wall-clock duration of one ingestion attempt.");
        _workerDurationSeconds = _meter.CreateHistogram<double>(
            "agentforge.worker.duration", unit: "s", description: "Wall-clock duration of one supervisor-graph worker, by worker.");
        _routingDecisionsTotal = _meter.CreateCounter<long>(
            "agentforge.routing_decisions", unit: "{decision}", description: "Supervisor routing decisions (handoffs), by from/to node.");
        _evidenceRetrievalsTotal = _meter.CreateCounter<long>(
            "agentforge.evidence_retrievals", unit: "{retrieval}", description: "Evidence-retrieval calls, by hit/miss outcome.");
        _evidenceRetrievalDurationSeconds = _meter.CreateHistogram<double>(
            "agentforge.evidence_retrieval.duration", unit: "s", description: "Wall-clock duration of one evidence-retrieval call.");
        _evidenceRetrievalResults = _meter.CreateHistogram<long>(
            "agentforge.evidence_retrieval.results", unit: "{snippet}", description: "Snippets returned by one evidence-retrieval call.");
        _rerankDurationSeconds = _meter.CreateHistogram<double>(
            "agentforge.rerank.duration", unit: "s", description: "Wall-clock duration of one rerank (cross-encoder) call.");
        _retrievalDegradationsTotal = _meter.CreateCounter<long>(
            "agentforge.retrieval_degradations", unit: "{degradation}",
            description: "Retrieval degradations (a half or the reranker failed and was skipped), by stage.");
    }

    /// <inheritdoc />
    public void RecordAgentTurn(bool succeeded, TimeSpan duration)
    {
        _agentTurnsTotal.Add(1, new KeyValuePair<string, object?>("outcome", succeeded ? "success" : "failure"));
        _agentTurnDurationSeconds.Record(duration.TotalSeconds);
    }

    /// <inheritdoc />
    public void RecordToolCall(string toolName, bool succeeded, TimeSpan duration)
    {
        _toolCallsTotal.Add(
            1,
            new KeyValuePair<string, object?>("tool", toolName),
            new KeyValuePair<string, object?>("outcome", succeeded ? "success" : "failure"));
        _toolCallDurationSeconds.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("tool", toolName));
    }

    /// <inheritdoc />
    public void RecordVerificationResult(bool passed) =>
        _verificationResultsTotal.Add(1, new KeyValuePair<string, object?>("outcome", passed ? "pass" : "fail"));

    /// <inheritdoc />
    public void RecordLlmUsage(int inputTokens, int outputTokens, decimal estimatedCostUsd)
    {
        _llmTokensTotal.Add(inputTokens, new KeyValuePair<string, object?>("direction", "input"));
        _llmTokensTotal.Add(outputTokens, new KeyValuePair<string, object?>("direction", "output"));
        _llmCostUsdTotal.Add((double)estimatedCostUsd);
    }

    /// <inheritdoc />
    public void RecordDocumentIngestion(string outcome, TimeSpan duration)
    {
        _documentIngestionsTotal.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        _documentIngestionDurationSeconds.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
    }

    /// <inheritdoc />
    public void RecordWorkerLatency(string worker, TimeSpan duration) =>
        _workerDurationSeconds.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("worker", worker));

    /// <inheritdoc />
    public void RecordRoutingDecision(string fromNode, string toNode) =>
        _routingDecisionsTotal.Add(
            1,
            new KeyValuePair<string, object?>("from", fromNode),
            new KeyValuePair<string, object?>("to", toNode));

    /// <inheritdoc />
    public void RecordEvidenceRetrieval(bool hit, int resultCount, TimeSpan duration)
    {
        var outcome = new KeyValuePair<string, object?>("outcome", hit ? "hit" : "miss");
        _evidenceRetrievalsTotal.Add(1, outcome);
        _evidenceRetrievalDurationSeconds.Record(duration.TotalSeconds);
        _evidenceRetrievalResults.Record(resultCount);
    }

    /// <inheritdoc />
    public void RecordRerankLatency(TimeSpan duration) => _rerankDurationSeconds.Record(duration.TotalSeconds);

    /// <inheritdoc />
    public void RecordRetrievalDegradation(string stage) =>
        _retrievalDegradationsTotal.Add(1, new KeyValuePair<string, object?>("stage", stage));

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
