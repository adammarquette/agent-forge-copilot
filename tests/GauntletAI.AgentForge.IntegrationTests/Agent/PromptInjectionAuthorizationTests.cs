using System.Text.Json;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.IntegrationTests.Mcp;
using GauntletAI.AgentForge.IntegrationTests.Support;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Observability;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.IntegrationTests.Agent;

/// <summary>
/// Exercises the real <see cref="McpToolDispatcher"/> in front of the real, QA-deployed
/// <see cref="McpToolServer"/> (<see cref="McpToolServerQaFixture"/>) with a hand-crafted,
/// adversarial tool call - a real model essentially never emits a <c>patientId</c>/<c>site</c>
/// argument on its own (<c>McpToolCatalog</c> never offers it in the schema), so simulating the
/// argument directly is the only reliable way to exercise FR-AUTH-3 without depending on
/// unpredictable LLM behavior. What a unit test's faked <see cref="IMcpToolServer"/> can't prove:
/// that the real end-to-end FHIR round trip still returns the session's own patient's real data -
/// not the injected target - and that the real <c>LoggerMessage</c> source-gen pipeline actually
/// writes the audit warning, not just a fake logger's in-memory list (tests/AGENTS.md - nothing
/// under test is mocked here except the argument itself, which is standing in for a compromised or
/// jailbroken model's output).
/// </summary>
[Trait("Metric", "M3-AuthorizationIntegrity")]
public sealed class PromptInjectionAuthorizationTests : IClassFixture<McpToolServerQaFixture>, IDisposable
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly McpToolServerQaFixture _toolServerFixture;
    private readonly CapturingLoggerProvider _capturedLogs = new();
    private readonly LoggerFactory _loggerFactory;
    private readonly McpToolDispatcher _dispatcher;

    public PromptInjectionAuthorizationTests(McpToolServerQaFixture toolServerFixture)
    {
        _toolServerFixture = toolServerFixture;
        _loggerFactory = new LoggerFactory([_capturedLogs]);
        _dispatcher = new McpToolDispatcher(
            _toolServerFixture.ToolServer, new AgentForgeMetrics(), _loggerFactory.CreateLogger<McpToolDispatcher>());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loggerFactory.Dispose();
        _capturedLogs.Dispose();
    }

    [Fact]
    public async Task DispatchAsync_ToolCallArgumentsCarryAnInjectedPatientId_IgnoresItReturnsTheRealSessionPatientAndLogsTheAttempt()
    {
        const string injectedPatientId = "definitely-not-the-session-patient-999999";
        var toolCall = new LlmToolCall("call-1", "get_patient_summary", $$"""{"patientId":"{{injectedPatientId}}"}""");

        var result = await _dispatcher.DispatchAsync(
            _toolServerFixture.OpenEmr.Options.Site, _toolServerFixture.OpenEmr.Options.TestPatientId!, toolCall, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var summary = JsonSerializer.Deserialize<PatientSummaryResult>(result.ResultJson, DeserializeOptions);
        summary!.Patient.Should().NotBeNull();
        summary.Patient!.Source.Id.Should().Be(
            _toolServerFixture.OpenEmr.Options.TestPatientId,
            "the dispatcher must force the session's own patient id onto every dispatch regardless of what the " +
            "tool call's arguments contain (FR-CHAT-3/FR-AUTH-3)");
        summary.Patient.Source.Id.Should().NotBe(injectedPatientId);

        _capturedLogs.Lines.Should().Contain(
            line => line.Contains("get_patient_summary", StringComparison.Ordinal) &&
                line.Contains("patientId", StringComparison.OrdinalIgnoreCase),
            "the suspicious-argument-override attempt must be logged, not just silently ignored (FR-AUTH-3)");
    }

    [Fact]
    public async Task DispatchAsync_ToolCallArgumentsCarryAnInjectedSite_IgnoresItAndLogsTheAttempt()
    {
        var toolCall = new LlmToolCall("call-2", "get_labs", """{"site":"some-other-site"}""");

        var result = await _dispatcher.DispatchAsync(
            _toolServerFixture.OpenEmr.Options.Site, _toolServerFixture.OpenEmr.Options.TestPatientId!, toolCall, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _capturedLogs.Lines.Should().Contain(
            line => line.Contains("get_labs", StringComparison.Ordinal) &&
                line.Contains("site", StringComparison.OrdinalIgnoreCase),
            "an injected 'site' field must be logged too, not just patientId (FR-AUTH-3)");
    }
}
