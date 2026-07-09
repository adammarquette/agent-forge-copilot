using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Llm;

namespace GauntletAI.AgentForge.UnitTests.Api.Session;

public sealed class InMemoryConversationStateStoreTests
{
    private readonly InMemoryConversationStateStore _sut = new();

    [Fact]
    public void TryGet_NothingSaved_ReturnsNull() =>
        _sut.TryGet("session-1").Should().BeNull();

    [Fact]
    public void Save_ThenTryGet_ReturnsTheSavedState()
    {
        var state = ConversationState.Start("default", "123");

        _sut.Save("session-1", state);

        _sut.TryGet("session-1").Should().Be(state);
    }

    [Fact]
    public void Save_CalledTwiceForSameSession_OverwritesRatherThanKeepingTheFirst()
    {
        _sut.Save("session-1", ConversationState.Start("default", "123"));
        var updated = ConversationState.Start("default", "123") with
        {
            Messages = [LlmMessage.FromText(LlmRole.User, "hello")],
        };

        _sut.Save("session-1", updated);

        _sut.TryGet("session-1").Should().Be(updated);
    }

    [Fact]
    public void TryGet_DifferentSessionId_DoesNotLeakAnotherSessionsState()
    {
        _sut.Save("session-1", ConversationState.Start("default", "123"));

        _sut.TryGet("session-2").Should().BeNull();
    }
}
