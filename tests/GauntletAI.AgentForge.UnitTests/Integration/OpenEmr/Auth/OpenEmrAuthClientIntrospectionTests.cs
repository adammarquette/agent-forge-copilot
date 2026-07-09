using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Auth;

public sealed class OpenEmrAuthClientIntrospectionTests
{
    [Fact]
    public async Task IntrospectAsync_ValidToken_SendsTokenWithAccessTokenHint()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        var expected = new IntrospectionResponse(true, "launch patient/patient.read", "sidecar-client", 1234567890, "user-1", "patient-1");
        IntrospectionRequest? captured = null;
        A.CallTo(() => api.IntrospectAsync("default", A<IntrospectionRequest>._, A<CancellationToken>._))
            .Invokes((string _, IntrospectionRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(expected));
        var client = new OpenEmrAuthClient(api);

        var result = await client.IntrospectAsync(
            "default", "access-token-abc", "sidecar-client", "the-secret", CancellationToken.None);

        result.Should().Be(expected);
        captured!.Token.Should().Be("access-token-abc");
        captured.TokenTypeHint.Should().Be("access_token");
        captured.ClientId.Should().Be("sidecar-client");
        captured.ClientSecret.Should().Be("the-secret");
    }

    [Fact]
    public async Task IntrospectAsync_Always_PassesCancellationTokenThrough()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        A.CallTo(() => api.IntrospectAsync(A<string>._, A<IntrospectionRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(false, null, null, null, null, null)));
        var client = new OpenEmrAuthClient(api);
        using var cts = new CancellationTokenSource();

        await client.IntrospectAsync("default", "token", "sidecar-client", null, cts.Token);

        A.CallTo(() => api.IntrospectAsync("default", A<IntrospectionRequest>._, cts.Token)).MustHaveHappenedOnceExactly();
    }
}
