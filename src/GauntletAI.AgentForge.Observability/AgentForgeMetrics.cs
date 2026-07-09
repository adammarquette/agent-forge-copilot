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
    public void Dispose() => _meter.Dispose();
}
