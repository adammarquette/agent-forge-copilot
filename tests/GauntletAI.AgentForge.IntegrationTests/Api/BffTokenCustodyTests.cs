using FluentAssertions;
using GauntletAI.AgentForge.Api.Chat;
using Microsoft.AspNetCore.SignalR.Client;
using static GauntletAI.AgentForge.IntegrationTests.Support.TaskWaitHelper;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Exercises the full real stack - real Anthropic, real QA OpenEMR, real SignalR hub - and proves
/// the one property a unit test faking every dependency cannot: that the clinician's real access
/// token never appears in anything the browser can see (ARCHITECTURE.md D11), across the actual
/// wire format the hub sends and the actual log lines the running host writes.
/// </summary>
public sealed class BffTokenCustodyTests : IClassFixture<BffQaFixture>
{
    private readonly BffQaFixture _fixture;

    public BffTokenCustodyTests(BffQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RequestBrief_RealSessionRealLlmRealOpenEmr_DeliversAnswerWithNoBearerTokenInPayloadOrLogs()
    {
        var cookies = await _fixture.SeedAuthenticatedSessionAsync(CancellationToken.None);
        await using var connection = _fixture.BuildHubConnection(cookies);

        var received = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<ChatMessage>("ChatMessage", received.SetResult);

        await connection.StartAsync(CancellationToken.None);
        await connection.InvokeAsync("RequestBrief", CancellationToken.None);

        var message = await WaitForAsync(received.Task);

        var realToken = _fixture.OpenEmr.Options.TestAccessToken!;
        message.PayloadJson.Should().NotContain(realToken, "the real access token must never reach the browser (ARCHITECTURE.md D11)");
        _fixture.CapturedLogs.Lines.Should().NotContain(
            line => line.Contains(realToken, StringComparison.Ordinal),
            "the token must never be written to a log line (ENGINEERING_STANDARDS.md §11)");
    }
}
