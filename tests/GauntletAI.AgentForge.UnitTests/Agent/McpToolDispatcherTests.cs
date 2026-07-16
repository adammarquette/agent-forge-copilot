using System.Net.Http;
using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Observability;
using GauntletAI.AgentForge.UnitTests.TestSupport;

namespace GauntletAI.AgentForge.UnitTests.Agent;

public sealed class McpToolDispatcherTests
{
    private readonly IMcpToolServer _toolServer = A.Fake<IMcpToolServer>();
    private readonly IAgentForgeMetrics _metrics = A.Fake<IAgentForgeMetrics>();
    private readonly CapturingLogger<McpToolDispatcher> _logger = new();
    private readonly McpToolDispatcher _sut;

    public McpToolDispatcherTests() => _sut = new McpToolDispatcher(_toolServer, _metrics, _logger);

    [Fact]
    public async Task DispatchAsync_GetPatientSummary_CallsToolServerWithSessionSiteAndPatientId()
    {
        var summary = new PatientSummaryResult(null, [], [], []);
        A.CallTo(() => _toolServer.GetPatientSummaryAsync(
                A<GetPatientSummaryRequest>.That.Matches(r => r.Site == "default" && r.PatientId == "1"),
                A<CancellationToken>._))
            .Returns(Task.FromResult(summary));

        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_1", "get_patient_summary", "{}"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.ToolUseId.Should().Be("call_1");
    }

    [Fact]
    public async Task DispatchAsync_GetLabsWithSinceDateArgument_ParsesArgumentAndPassesItThrough()
    {
        GetLabsRequest? captured = null;
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Invokes((GetLabsRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(new LabsResult([])));

        await _sut.DispatchAsync("default", "1", new LlmToolCall("call_2", "get_labs", """{"since_date":"ge2026-01-01"}"""), CancellationToken.None);

        captured!.SinceDate.Should().Be("ge2026-01-01");
    }

    [Fact]
    public async Task DispatchAsync_GetLabsWithEmptyArguments_CallsToolServerWithNullSinceDate()
    {
        GetLabsRequest? captured = null;
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Invokes((GetLabsRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(new LabsResult([])));

        await _sut.DispatchAsync("default", "1", new LlmToolCall("call_3", "get_labs", "{}"), CancellationToken.None);

        captured!.SinceDate.Should().BeNull();
    }

    [Fact]
    public async Task DispatchAsync_ArgumentsContainAPatientIdField_IgnoresItAndUsesSessionPatientId()
    {
        // The single most important test in this file: even if a tool call's arguments somehow
        // carry a patientId (the model shouldn't be able to produce one - McpToolCatalog's schemas
        // don't offer it - but nothing stops a determined prompt-injection attempt from trying),
        // the dispatcher must never let it override the session's bound patient. Enforcement below
        // the model, not a schema the model could be talked out of respecting (FR-CHAT-3, UC-4).
        GetLabsRequest? captured = null;
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Invokes((GetLabsRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(new LabsResult([])));

        await _sut.DispatchAsync(
            "default", "1", new LlmToolCall("call_4", "get_labs", """{"patientId":"999","since_date":"ge2026-01-01"}"""), CancellationToken.None);

        captured!.PatientId.Should().Be("1");
    }

    [Fact]
    public async Task DispatchAsync_ArgumentsContainAPatientIdField_LogsASuspiciousOverrideAttempt()
    {
        // FR-AUTH-3: the override is already ignored functionally (test above) - this proves the
        // attempt itself is also recorded, satisfying "confirmed... and get logged," not just
        // "confirmed to yield zero unauthorized disclosure."
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LabsResult([])));

        await _sut.DispatchAsync(
            "default", "1", new LlmToolCall("call_4", "get_labs", """{"patientId":"999","since_date":"ge2026-01-01"}"""), CancellationToken.None);

        _logger.Lines.Should().ContainSingle(line =>
            line.Contains("get_labs", StringComparison.Ordinal) && line.Contains("patientId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DispatchAsync_ArgumentsContainNoUnexpectedFields_LogsNothingSuspicious()
    {
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LabsResult([])));

        await _sut.DispatchAsync(
            "default", "1", new LlmToolCall("call_4", "get_labs", """{"since_date":"ge2026-01-01"}"""), CancellationToken.None);

        _logger.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_UnknownToolName_ReturnsErrorResultRatherThanThrowing()
    {
        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_5", "delete_everything", "{}"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.ToolUseId.Should().Be("call_5");
        A.CallTo(_toolServer).MustNotHaveHappened();
    }

    [Fact]
    public async Task DispatchAsync_MalformedArgumentsJson_ReturnsErrorResultRatherThanThrowing()
    {
        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_6", "get_labs", "not valid json"), CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_IntervalChangesWithoutRequiredSinceDate_ReturnsErrorResultRatherThanThrowing()
    {
        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_7", "get_interval_changes", "{}"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        A.CallTo(_toolServer).MustNotHaveHappened();
    }

    [Fact]
    public async Task DispatchAsync_ToolServerThrowsContractException_ReturnsErrorResultRatherThanThrowing()
    {
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Throws(new McpToolContractException("get_labs", ["bad input"]));

        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_8", "get_labs", "{}"), CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_ToolServerThrowsHttpRequestException_DegradesToUnavailableWithoutLeakingRawHttpStatus()
    {
        // An upstream FHIR/HTTP failure (e.g. a 403 for a resource the token can't read) must read
        // as a clean "unavailable" in the brief, not leak raw "Response status code..." text; the
        // agent still sees IsError:true and won't fabricate (UC-5). reference: gitlab#76
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Throws(new HttpRequestException("Response status code does not indicate success: 403 (Forbidden)."));

        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_403", "get_labs", "{}"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.ResultJson.Should().NotContain("403").And.NotContain("Response status code");
        result.ResultJson.Should().Contain("unavailable");
    }

    [Fact]
    public async Task DispatchAsync_GetRecentEncountersWithCountArgument_ParsesItThrough()
    {
        GetRecentEncountersRequest? captured = null;
        A.CallTo(() => _toolServer.GetRecentEncountersAsync(A<GetRecentEncountersRequest>._, A<CancellationToken>._))
            .Invokes((GetRecentEncountersRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(new RecentEncountersResult([])));

        await _sut.DispatchAsync("default", "1", new LlmToolCall("call_9", "get_recent_encounters", """{"count":5}"""), CancellationToken.None);

        captured!.Count.Should().Be(5);
    }

    [Fact]
    public async Task DispatchAsync_ToolServerSucceeds_RecordsAToolCallMetricTaggedSuccess()
    {
        A.CallTo(() => _toolServer.GetPatientSummaryAsync(A<GetPatientSummaryRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PatientSummaryResult(null, [], [], [])));

        await _sut.DispatchAsync("default", "1", new LlmToolCall("call_11", "get_patient_summary", "{}"), CancellationToken.None);

        A.CallTo(() => _metrics.RecordToolCall("get_patient_summary", true, A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DispatchAsync_ToolServerThrows_RecordsAToolCallMetricTaggedFailure()
    {
        A.CallTo(() => _toolServer.GetLabsAsync(A<GetLabsRequest>._, A<CancellationToken>._))
            .Throws(new McpToolContractException("get_labs", ["bad input"]));

        await _sut.DispatchAsync("default", "1", new LlmToolCall("call_12", "get_labs", "{}"), CancellationToken.None);

        A.CallTo(() => _metrics.RecordToolCall("get_labs", false, A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DispatchAsync_Always_PassesCancellationTokenThrough()
    {
        A.CallTo(() => _toolServer.GetPatientSummaryAsync(A<GetPatientSummaryRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PatientSummaryResult(null, [], [], [])));
        using var cts = new CancellationTokenSource();

        await _sut.DispatchAsync("default", "1", new LlmToolCall("call_10", "get_patient_summary", "{}"), cts.Token);

        A.CallTo(() => _toolServer.GetPatientSummaryAsync(A<GetPatientSummaryRequest>._, cts.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DispatchAsync_GetDocumentFacts_DelegatesToTheDocumentFactsToolWithTheSessionPatientId()
    {
        var tool = A.Fake<IDocumentFactsTool>();
        A.CallTo(() => tool.GetAsync("1", A<CancellationToken>._))
            .Returns(Task.FromResult(new DocumentFactsResult(
                [new DocumentFactRecord("Document", "abc12345", "lab.result", "Potassium 5.9", "docref-9", "1")])));
        var sut = new McpToolDispatcher(_toolServer, _metrics, _logger, tool);

        var result = await sut.DispatchAsync("default", "1", new LlmToolCall("call_df", "get_document_facts", "{}"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.ResultJson.Should().Contain("Document").And.Contain("docref-9");
        A.CallTo(() => tool.GetAsync("1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DispatchAsync_GetDocumentFactsWithNoToolWired_ReturnsAnEmptyResultRatherThanThrowing()
    {
        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_df2", "get_document_facts", "{}"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.ResultJson.Should().Contain("Facts");
    }

    [Fact]
    public async Task DispatchAsync_RetrieveEvidence_DelegatesToTheEvidenceToolWithTheQuery()
    {
        var tool = A.Fake<IEvidenceTool>();
        A.CallTo(() => tool.GetAsync("inr target", A<CancellationToken>._))
            .Returns(Task.FromResult(new EvidenceResult(
                [new EvidenceRecord("Guideline", "chunk-1", "anticoag-2026", "Warfarin INR target", "Target INR 2.0 to 3.0.")])));
        var sut = new McpToolDispatcher(_toolServer, _metrics, _logger, null, tool);

        var result = await sut.DispatchAsync("default", "1", new LlmToolCall("call_re", "retrieve_evidence", """{"query":"inr target"}"""), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.ResultJson.Should().Contain("Guideline").And.Contain("chunk-1");
        A.CallTo(() => tool.GetAsync("inr target", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DispatchAsync_RetrieveEvidenceWithoutQuery_ReturnsErrorResultWithoutCallingTheTool()
    {
        var tool = A.Fake<IEvidenceTool>();
        var sut = new McpToolDispatcher(_toolServer, _metrics, _logger, null, tool);

        var result = await sut.DispatchAsync("default", "1", new LlmToolCall("call_re2", "retrieve_evidence", "{}"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        A.CallTo(() => tool.GetAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DispatchAsync_RetrieveEvidenceWithNoToolWired_ReturnsAnEmptyResultRatherThanThrowing()
    {
        var result = await _sut.DispatchAsync("default", "1", new LlmToolCall("call_re3", "retrieve_evidence", """{"query":"inr target"}"""), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.ResultJson.Should().Contain("Snippets");
    }
}
