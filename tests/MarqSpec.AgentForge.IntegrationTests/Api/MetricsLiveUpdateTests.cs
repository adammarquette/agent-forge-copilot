using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Chat;
using Microsoft.AspNetCore.SignalR.Client;
using static MarqSpec.AgentForge.IntegrationTests.Support.TaskWaitHelper;

namespace MarqSpec.AgentForge.IntegrationTests.Api;

/// <summary>
/// The automatable half of FR-OBS-3's "dashboard reflects live traffic during a load test": fires
/// several real chat turns - real LLM, real tool dispatch against real QA OpenEMR - against the
/// real host, then confirms <c>/metrics</c> (the exact endpoint Prometheus/Grafana scrape,
/// <c>observability/docker-compose.yml</c>) actually reflects them. Whether Grafana's panel
/// visibly updates is a manual/visual confirmation documented in <c>observability/README.md</c> -
/// this test automates the part that determines it either way: does the data source the dashboard
/// reads from actually update with live traffic, or is it stale/frozen.
/// </summary>
public sealed class MetricsLiveUpdateTests : IClassFixture<BffQaFixture>
{
    private static readonly Regex MetricLinePattern = new(
        @"^agentforge_agent_turns_total(\{[^}]*\})?\s+(?<value>[0-9.eE+-]+)$", RegexOptions.Multiline);

    private readonly BffQaFixture _fixture;

    public MetricsLiveUpdateTests(BffQaFixture fixture) => _fixture = fixture;

    [Fact(Skip = BffQaFixture.ChatHubSessionSkipReason)]
    public async Task Metrics_AfterSeveralRealChatTurns_ReflectsAllOfThem()
    {
        var cookies = await _fixture.SeedAuthenticatedSessionAsync(CancellationToken.None);
        using var httpClient = _fixture.CreateHttpClient(cookies);

        var before = await GetAgentTurnsTotalAsync(httpClient);

        const int turnCount = 2;
        for (var i = 0; i < turnCount; i++)
        {
            await using var connection = _fixture.BuildHubConnection(cookies);
            var received = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<ChatMessage>("ChatMessage", received.SetResult);
            await connection.StartAsync(CancellationToken.None);
            await connection.InvokeAsync("RequestBrief", CancellationToken.None);
            await WaitForAsync(received.Task);
            await connection.StopAsync(CancellationToken.None);
        }

        var after = await GetAgentTurnsTotalAsync(httpClient);

        (after - before).Should().BeGreaterThanOrEqualTo(
            turnCount,
            $"{turnCount} real chat turns were just dispatched above - if /metrics doesn't reflect at least that " +
            "many new agent_turns, the dashboard Prometheus/Grafana read from is not actually live");
    }

    private static async Task<double> GetAgentTurnsTotalAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/metrics", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the Prometheus scrape endpoint must be reachable on the real host");
        var body = await response.Content.ReadAsStringAsync();

        return MetricLinePattern.Matches(body)
            .Sum(match => double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture));
    }
}
