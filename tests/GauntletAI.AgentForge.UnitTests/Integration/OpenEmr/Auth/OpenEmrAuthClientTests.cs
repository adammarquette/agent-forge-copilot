using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Auth;

public sealed class OpenEmrAuthClientTests
{
    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_PublicClientNullSecret_SendsEmptyStringNotNull()
    {
        // A public client's registered secret is an empty string, not absent (same server behavior
        // confirmed for introspection - see OpenEmrAuthClientIntrospectionTests). A null ClientSecret
        // must never reach Refit's UrlEncoded body serializer, which omits null properties entirely -
        // coerce to string.Empty so the form still carries client_secret=.
        var api = A.Fake<IOpenEmrAuthApi>();
        var expectedResponse = new TokenResponse("access-abc", "Bearer", 3600, "launch patient/patient.read", "refresh-xyz", "123", null);
        TokenRequest? capturedRequest = null;
        A.CallTo(() => api.ExchangeTokenAsync("default", A<TokenRequest>._, A<CancellationToken>._))
            .Invokes((string _, TokenRequest req, CancellationToken _) => capturedRequest = req)
            .Returns(Task.FromResult(expectedResponse));
        var client = new OpenEmrAuthClient(api);

        var result = await client.ExchangeAuthorizationCodeAsync(
            site: "default",
            code: "auth-code-123",
            redirectUri: "https://sidecar.example.org/callback",
            clientId: "sidecar-client",
            codeVerifier: "verifier-abc",
            clientSecret: null,
            CancellationToken.None);

        result.Should().Be(expectedResponse);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.GrantType.Should().Be("authorization_code");
        capturedRequest.Code.Should().Be("auth-code-123");
        capturedRequest.RedirectUri.Should().Be("https://sidecar.example.org/callback");
        capturedRequest.ClientId.Should().Be("sidecar-client");
        capturedRequest.CodeVerifier.Should().Be("verifier-abc");
        capturedRequest.ClientSecret.Should().Be(string.Empty);
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_ConfidentialClient_IncludesClientSecretInRequest()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        TokenRequest? capturedRequest = null;
        A.CallTo(() => api.ExchangeTokenAsync(A<string>._, A<TokenRequest>._, A<CancellationToken>._))
            .Invokes((string _, TokenRequest req, CancellationToken _) => capturedRequest = req)
            .Returns(Task.FromResult(new TokenResponse("a", "Bearer", null, null, null, null, null)));
        var client = new OpenEmrAuthClient(api);

        await client.ExchangeAuthorizationCodeAsync(
            site: "default",
            code: "auth-code-123",
            redirectUri: "https://sidecar.example.org/callback",
            clientId: "confidential-client",
            codeVerifier: "verifier-abc",
            clientSecret: "shh-its-a-secret",
            CancellationToken.None);

        capturedRequest!.ClientSecret.Should().Be("shh-its-a-secret");
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_Always_PassesCancellationTokenThrough()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        A.CallTo(() => api.ExchangeTokenAsync(A<string>._, A<TokenRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("a", "Bearer", null, null, null, null, null)));
        var client = new OpenEmrAuthClient(api);
        using var cts = new CancellationTokenSource();

        await client.ExchangeAuthorizationCodeAsync(
            "default", "code", "https://sidecar.example.org/callback", "client", "verifier", null, cts.Token);

        A.CallTo(() => api.ExchangeTokenAsync("default", A<TokenRequest>._, cts.Token)).MustHaveHappenedOnceExactly();
    }
}
