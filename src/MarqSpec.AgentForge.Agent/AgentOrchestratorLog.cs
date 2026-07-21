using MarqSpec.AgentForge.Llm;
using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.Agent;

/// <summary>
/// Source-generated log messages for <see cref="AgentOrchestrator"/> (CA1848). Tool names and
/// error flags only - never a clinical value or patient identifier (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class AgentOrchestratorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Tool call dispatched: {ToolName}, error={IsError}")]
    public static partial void ToolCallDispatched(ILogger logger, string toolName, bool isError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Degraded to deterministic fallback: {Reason}")]
    public static partial void DegradedToDeterministicFallback(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected malformed model output (stopReason={StopReason}, hasContent={HasContent}); attempting one repair.")]
    public static partial void MalformedOutputRepairAttempt(ILogger logger, LlmStopReason stopReason, bool hasContent);

    // The per-encounter story (FR-OBS-W2-1): the whole turn's telemetry on one correlation-scoped line so any
    // past encounter is answerable in Grafana - tool sequence, step + turn latency, tokens, cost, retrieval
    // hits, extraction confidence, and the runtime verification (eval) outcome. Tool names + numbers only,
    // never a clinical value or patient id (ENGINEERING_STANDARDS.md §7). reference: documentation/W2_PRD.md.
    [LoggerMessage(Level = LogLevel.Information, EventId = 9001, Message =
        "encounter.telemetry tools=[{ToolSequence}] rounds={Rounds} turn_ms={TurnMs} tool_ms=[{ToolLatencyMs}] " +
        "in_tokens={InputTokens} out_tokens={OutputTokens} cost_usd={CostUsd} retrieval_hits={RetrievalHits} " +
        "extraction_confidence={ExtractionConfidence} verification_passed={VerificationPassed} suppressed_claims={SuppressedClaims}")]
    public static partial void EncounterTelemetry(
        ILogger logger, string toolSequence, int rounds, long turnMs, string toolLatencyMs,
        long inputTokens, long outputTokens, double costUsd, int? retrievalHits, double? extractionConfidence,
        bool verificationPassed, int suppressedClaims);
}
