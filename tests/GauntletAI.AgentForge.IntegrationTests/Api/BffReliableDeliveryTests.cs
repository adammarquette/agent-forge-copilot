using FluentAssertions;
using GauntletAI.AgentForge.Api.Chat;
using Microsoft.AspNetCore.SignalR.Client;
using static GauntletAI.AgentForge.IntegrationTests.Support.TaskWaitHelper;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Exercises reconnect-and-resume against the real hub (ENGINEERING_STANDARDS.md §12) - a unit
/// test of <c>InMemoryChatMessageOutbox</c> alone proves the data structure; only a real dropped
/// SignalR connection followed by a real new one proves the resume path actually recovers a
/// message the first connection never got to acknowledge.
/// </summary>
public sealed class BffReliableDeliveryTests : IClassFixture<BffQaFixture>
{
    private readonly BffQaFixture _fixture;

    public BffReliableDeliveryTests(BffQaFixture fixture) => _fixture = fixture;

    [Fact(Skip = BffQaFixture.ChatHubSessionSkipReason)]
    public async Task Reconnect_NewConnectionSameSession_ResumeReturnsMessageSentToThePriorConnection()
    {
        var cookies = await _fixture.SeedAuthenticatedSessionAsync(CancellationToken.None);
        long deliveredSequence;

        await using (var connection1 = _fixture.BuildHubConnection(cookies))
        {
            var received = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection1.On<ChatMessage>("ChatMessage", received.SetResult);
            await connection1.StartAsync(CancellationToken.None);
            await connection1.InvokeAsync("RequestBrief", CancellationToken.None);

            var message = await WaitForAsync(received.Task);
            deliveredSequence = message.Sequence;

            // Simulate a drop: stop without ever calling Resume on this connection.
            await connection1.StopAsync(CancellationToken.None);
        }

        await using var connection2 = _fixture.BuildHubConnection(cookies);
        await connection2.StartAsync(CancellationToken.None);

        var resumed = await connection2.InvokeAsync<List<ChatMessage>>("Resume", 0L, CancellationToken.None);

        resumed.Should().Contain(
            m => m.Sequence == deliveredSequence,
            "a reconnect on the same session must be able to replay a message sent to the prior, now-dead connection");
    }

    [Fact(Skip = BffQaFixture.ChatHubSessionSkipReason)]
    public async Task Resume_CalledAgainWithTheSequenceAlreadySeen_ReturnsNothingNewTheSecondTime()
    {
        var cookies = await _fixture.SeedAuthenticatedSessionAsync(CancellationToken.None);
        await using var connection = _fixture.BuildHubConnection(cookies);
        var received = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<ChatMessage>("ChatMessage", received.SetResult);
        await connection.StartAsync(CancellationToken.None);
        await connection.InvokeAsync("RequestBrief", CancellationToken.None);
        var message = await WaitForAsync(received.Task);

        var firstResume = await connection.InvokeAsync<List<ChatMessage>>("Resume", 0L, CancellationToken.None);
        var secondResume = await connection.InvokeAsync<List<ChatMessage>>("Resume", message.Sequence, CancellationToken.None);

        firstResume.Should().Contain(m => m.Sequence == message.Sequence);
        secondResume.Should().BeEmpty("resuming from the sequence already seen must not redeliver it - the idempotent-resume contract");
    }
}
