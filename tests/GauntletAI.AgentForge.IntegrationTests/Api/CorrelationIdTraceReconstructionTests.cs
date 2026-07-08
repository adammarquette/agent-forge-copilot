using System.Text.RegularExpressions;
using FluentAssertions;
using GauntletAI.AgentForge.Api.Chat;
using Microsoft.AspNetCore.SignalR.Client;
using static GauntletAI.AgentForge.IntegrationTests.Support.TaskWaitHelper;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Proves FR-OBS-1's "correlation id flows as a logging scope on every downstream call" against
/// the real host, not the unit suite's own hand-rolled logger: <see cref="BffQaFixture"/> runs the
/// actual composition root, whose real <c>ILoggerFactory</c> shares one scope stack across every
/// category (<c>ChatSessionCoordinator</c>, <c>AgentOrchestrator</c>, <c>McpToolDispatcher</c>,
/// <c>AuditingMcpToolServer</c>) the way a hand-rolled per-component test double cannot. A real
/// pre-visit brief - real LLM, real tool dispatch against real QA OpenEMR - reconstructed purely
/// from the captured log stream, the same discipline as Epic 8's audit-trail-reconstruction test.
/// </summary>
public sealed class CorrelationIdTraceReconstructionTests : IClassFixture<BffQaFixture>
{
    private static readonly Regex CorrelationScopePattern = new(@"CorrelationId=(?<id>\S+)", RegexOptions.Compiled);

    private readonly BffQaFixture _fixture;

    public CorrelationIdTraceReconstructionTests(BffQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RequestBrief_RealSessionRealLlmRealOpenEmr_EveryScopedLogLineForThisTurnCarriesTheSameCorrelationId()
    {
        var cookies = await _fixture.SeedAuthenticatedSessionAsync(CancellationToken.None);
        await using var connection = _fixture.BuildHubConnection(cookies);
        var received = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<ChatMessage>("ChatMessage", received.SetResult);

        await connection.StartAsync(CancellationToken.None);
        await connection.InvokeAsync("RequestBrief", CancellationToken.None);
        await WaitForAsync(received.Task);

        var correlationIds = _fixture.CapturedLogs.Lines
            .Select(line => CorrelationScopePattern.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToList();

        correlationIds.Should().NotBeEmpty(
            "the correlation-id scope ChatSessionCoordinator opens must actually reach real captured log lines, " +
            "not just theoretically be pushed - if this is empty, nothing downstream is logging inside the scope " +
            "or the scope isn't propagating");
        correlationIds.Distinct().Should().ContainSingle(
            "every log line produced while handling this one request must carry the same correlation id - a " +
            "reconstructable trace requires one coherent id per request, not several different or missing ones");
    }
}
