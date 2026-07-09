using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Auth;

public sealed class OpenEmrAuthClientRefreshTests
{
    [Fact]
    public async Task RefreshAccessTokenAsync_PublicClient_SendsRefreshTokenGrant()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        var expectedResponse = new TokenResponse("access-fresh", "Bearer", 3600, "launch/patient patient/Patient.read offline_access", "refresh-xyz", "patient-123", null);
        TokenRequest? capturedRequest = null;
        A.CallTo(() => api.ExchangeTokenAsync("default", A<TokenRequest>._, A<CancellationToken>._))
            .Invokes((string _, TokenRequest req, CancellationToken _) => capturedRequest = req)
            .Returns(Task.FromResult(expectedResponse));
        var client = new OpenEmrAuthClient(api);

        var result = await client.RefreshAccessTokenAsync(
            site: "default",
            refreshToken: "refresh-xyz",
            clientId: "sidecar-client",
            clientSecret: null,
            CancellationToken.None);

        result.Should().Be(expectedResponse);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.GrantType.Should().Be("refresh_token");
        capturedRequest.RefreshToken.Should().Be("refresh-xyz");
        capturedRequest.ClientId.Should().Be("sidecar-client");
        capturedRequest.ClientSecret.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_ConfidentialClient_IncludesClientSecretInRequest()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        TokenRequest? capturedRequest = null;
        A.CallTo(() => api.ExchangeTokenAsync(A<string>._, A<TokenRequest>._, A<CancellationToken>._))
            .Invokes((string _, TokenRequest req, CancellationToken _) => capturedRequest = req)
            .Returns(Task.FromResult(new TokenResponse("a", "Bearer", null, null, null, null, null)));
        var client = new OpenEmrAuthClient(api);

        await client.RefreshAccessTokenAsync(
            site: "default",
            refreshToken: "refresh-xyz",
            clientId: "confidential-client",
            clientSecret: "shh-its-a-secret",
            CancellationToken.None);

        capturedRequest!.ClientSecret.Should().Be("shh-its-a-secret");
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_Always_PassesCancellationTokenThrough()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        A.CallTo(() => api.ExchangeTokenAsync(A<string>._, A<TokenRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("a", "Bearer", null, null, null, null, null)));
        var client = new OpenEmrAuthClient(api);
        using var cts = new CancellationTokenSource();

        await client.RefreshAccessTokenAsync("default", "refresh-xyz", "client", null, cts.Token);

        A.CallTo(() => api.ExchangeTokenAsync("default", A<TokenRequest>._, cts.Token)).MustHaveHappenedOnceExactly();
    }
}
