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

    [Fact]
    public async Task AccessToken_SetOnOneInstance_IsVisibleFromADifferentInstanceOnTheSameLogicalFlow()
    {
        // Regression test (GitLab issue #39, follow-up): AuthHandler is constructed by
        // IHttpClientFactory using ITS OWN internal scope, not the caller's DI scope - a plain
        // per-instance field means AuthHandler's ScopedAccessTokenProvider is *never* the same
        // object ChatSessionCoordinator set the token on, no matter the handler's lifetime.
        // Confirmed live 2026-07-10: every real tool call still failed FR-AUTH-1's check even
        // after forcing frequent handler rebuilds. Storage must be ambient to the *logical async
        // flow*, not tied to which DI scope constructed which instance.
        var setBySessionCoordinator = new ScopedAccessTokenProvider { AccessToken = "token-abc" };
        var readByAuthHandler = new ScopedAccessTokenProvider();

        var result = await readByAuthHandler.GetAccessTokenAsync(CancellationToken.None);

        result.Should().Be("token-abc");
    }

    [Fact]
    public async Task AccessToken_SetInOneConcurrentFlow_NeverLeaksIntoAnother()
    {
        // The other half of the same fix: ambient storage must stay isolated per logical async
        // flow (one real hub invocation), or one clinician's session could attach a *different*
        // clinician's token to a concurrent request - a genuine cross-patient data exposure, not
        // just a correctness bug.
        var flowAResult = new TaskCompletionSource<string?>();
        var flowBResult = new TaskCompletionSource<string?>();

        var flowA = Task.Run(async () =>
        {
            var provider = new ScopedAccessTokenProvider { AccessToken = "token-a" };
            await Task.Delay(50);
            flowAResult.SetResult(await provider.GetAccessTokenAsync(CancellationToken.None));
        });
        var flowB = Task.Run(async () =>
        {
            var provider = new ScopedAccessTokenProvider { AccessToken = "token-b" };
            await Task.Delay(50);
            flowBResult.SetResult(await provider.GetAccessTokenAsync(CancellationToken.None));
        });

        await Task.WhenAll(flowA, flowB);

        (await flowAResult.Task).Should().Be("token-a");
        (await flowBResult.Task).Should().Be("token-b");
    }
}
