using FluentAssertions;
using GauntletAI.AgentForge.Api.Chat;

namespace GauntletAI.AgentForge.UnitTests.Api.Chat;

public sealed class InMemoryChatMessageOutboxTests
{
    private readonly InMemoryChatMessageOutbox _sut = new();

    [Fact]
    public void Append_FirstMessage_AssignsAPositiveSequence() =>
        _sut.Append("session-1", "brief", "{}").Sequence.Should().BePositive();

    [Fact]
    public void Append_MultipleMessages_AssignsStrictlyIncreasingSequence()
    {
        var first = _sut.Append("session-1", "brief", "{}");
        var second = _sut.Append("session-1", "answer", "{}");

        second.Sequence.Should().BeGreaterThan(first.Sequence);
    }

    [Fact]
    public void GetSince_LastSeenZero_ReturnsEveryMessageForThatSessionInOrder()
    {
        var first = _sut.Append("session-1", "brief", "{}");
        var second = _sut.Append("session-1", "answer", "{}");

        var result = _sut.GetSince("session-1", 0);

        result.Should().Equal(first, second);
    }

    [Fact]
    public void GetSince_LastSeenMidway_ReturnsOnlyMessagesAfterIt()
    {
        var first = _sut.Append("session-1", "brief", "{}");
        var second = _sut.Append("session-1", "answer", "{}");

        var result = _sut.GetSince("session-1", first.Sequence);

        result.Should().Equal(second);
    }

    [Fact]
    public void GetSince_UnknownSession_ReturnsEmptyRatherThanThrowing() =>
        _sut.GetSince("never-appended-to", 0).Should().BeEmpty();

    [Fact]
    public void GetSince_DifferentSession_DoesNotLeakMessagesAcrossSessions()
    {
        _sut.Append("session-1", "brief", "{}");

        _sut.GetSince("session-2", 0).Should().BeEmpty();
    }

    [Fact]
    public void Append_ExceedsBoundPerSession_EvictsOldestButRecentMessagesRemainRetrievable()
    {
        // Bounded so an abandoned session can't grow the outbox forever; a session reconnecting
        // this far behind is treated as abandoned, not "briefly dropped" (documented trade-off).
        ChatMessage? last = null;
        for (var i = 0; i < 60; i++)
        {
            last = _sut.Append("session-1", "answer", "{}");
        }

        var result = _sut.GetSince("session-1", 0);

        result.Should().HaveCountLessThan(60);
        result.Should().Contain(last!);
    }
}
