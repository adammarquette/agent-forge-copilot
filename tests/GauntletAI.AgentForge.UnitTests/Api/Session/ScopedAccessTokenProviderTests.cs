using FluentAssertions;
using GauntletAI.AgentForge.Api.Session;

namespace GauntletAI.AgentForge.UnitTests.Api.Session;

public sealed class ScopedAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_AccessTokenSet_ReturnsIt()
    {
        var sut = new ScopedAccessTokenProvider { AccessToken = "token-abc" };

        var result = await sut.GetAccessTokenAsync(CancellationToken.None);

        result.Should().Be("token-abc");
    }

    [Fact]
    public async Task GetAccessTokenAsync_NeverSet_ReturnsNull()
    {
        // A hub invocation that never authenticates the session must never fall back to
        // attaching a stale or default token to an outbound FHIR call.
        var sut = new ScopedAccessTokenProvider();

        var result = await sut.GetAccessTokenAsync(CancellationToken.None);

        result.Should().BeNull();
    }
}
