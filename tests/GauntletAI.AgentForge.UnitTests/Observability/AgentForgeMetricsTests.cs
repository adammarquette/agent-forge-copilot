using System.Diagnostics.Metrics;
using FluentAssertions;
using GauntletAI.AgentForge.Observability;

namespace GauntletAI.AgentForge.UnitTests.Observability;

/// <summary>
/// Verifies <see cref="AgentForgeMetrics"/> actually records through the real
/// <see cref="Meter"/>/<see cref="Instrument"/> APIs, using a real <see cref="MeterListener"/> -
/// the standard BCL way to observe metrics without a live exporter/collector - rather than trusting
/// the plumbing by inspection.
/// </summary>
public sealed class AgentForgeMetricsTests : IDisposable
{
    private readonly List<Measurement> _measurements = [];
    private readonly MeterListener _listener;
    private readonly AgentForgeMetrics _sut = new();

    public AgentForgeMetricsTests()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AgentForgeMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _measurements.Add(new Measurement(instrument.Name, value, ToDictionary(tags))));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            _measurements.Add(new Measurement(instrument.Name, value, ToDictionary(tags))));
        _listener.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _listener.Dispose();
        _sut.Dispose();
    }

    [Fact]
    public void RecordAgentTurn_Succeeded_RecordsACountTaggedSuccessAndTheDuration()
    {
        _sut.RecordAgentTurn(true, TimeSpan.FromSeconds(1.5));

        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.agent_turns" && (string)m.Tags["outcome"]! == "success");
        _measurements.Should().Contain(m => m.InstrumentName == "agentforge.agent_turn.duration" && (double)m.Value == 1.5);
    }

    [Fact]
    public void RecordAgentTurn_Failed_RecordsACountTaggedFailure()
    {
        _sut.RecordAgentTurn(false, TimeSpan.FromSeconds(0.5));

        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.agent_turns" && (string)m.Tags["outcome"]! == "failure");
    }

    [Fact]
    public void RecordToolCall_Succeeded_RecordsACountAndDurationTaggedWithTheToolName()
    {
        _sut.RecordToolCall("get_labs", true, TimeSpan.FromMilliseconds(250));

        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.tool_calls" &&
            (string)m.Tags["tool"]! == "get_labs" &&
            (string)m.Tags["outcome"]! == "success");
        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.tool_call.duration" && (string)m.Tags["tool"]! == "get_labs");
    }

    [Fact]
    public void RecordVerificationResult_Passed_RecordsACountTaggedPass()
    {
        _sut.RecordVerificationResult(true);

        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.verification_results" && (string)m.Tags["outcome"]! == "pass");
    }

    [Fact]
    public void RecordVerificationResult_Failed_RecordsACountTaggedFail()
    {
        _sut.RecordVerificationResult(false);

        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.verification_results" && (string)m.Tags["outcome"]! == "fail");
    }

    [Fact]
    public void RecordLlmUsage_Always_RecordsInputAndOutputTokensSeparatelyPlusCost()
    {
        _sut.RecordLlmUsage(inputTokens: 100, outputTokens: 40, estimatedCostUsd: 0.02m);

        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.llm_tokens" && (string)m.Tags["direction"]! == "input" && (long)m.Value == 100);
        _measurements.Should().Contain(m =>
            m.InstrumentName == "agentforge.llm_tokens" && (string)m.Tags["direction"]! == "output" && (long)m.Value == 40);
        _measurements.Should().Contain(m => m.InstrumentName == "agentforge.llm_cost_usd" && (double)m.Value == 0.02);
    }

    private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Dictionary<string, object?> dictionary = [];
        foreach (var tag in tags)
        {
            dictionary[tag.Key] = tag.Value;
        }

        return dictionary;
    }

    private sealed record Measurement(string InstrumentName, object Value, IReadOnlyDictionary<string, object?> Tags);
}
