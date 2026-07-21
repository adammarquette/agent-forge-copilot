using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Auth;

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

    [Fact]
    public async Task IntrospectAsync_NullClientSecret_SendsEmptyStringNotNull()
    {
        // Regression test (GitLab issue TBD): a public client's registered secret is an empty
        // string, not absent - the introspection endpoint authenticates the caller by matching
        // client_secret exactly, including empty-string-to-empty-string. Refit's UrlEncoded body
        // serialization omits null properties entirely, which the live server treats as an
        // unauthenticated call and silently returns {"active":false} rather than erroring, so a
        // null ClientSecret here must never reach the wire as "the field is missing" - it must be
        // coerced to string.Empty so the form still carries client_secret=.
        var api = A.Fake<IOpenEmrAuthApi>();
        IntrospectionRequest? captured = null;
        A.CallTo(() => api.IntrospectAsync("default", A<IntrospectionRequest>._, A<CancellationToken>._))
            .Invokes((string _, IntrospectionRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(new IntrospectionResponse(false, null, null, null, null, null)));
        var client = new OpenEmrAuthClient(api);

        await client.IntrospectAsync("default", "access-token-abc", "public-client", null, CancellationToken.None);

        captured!.ClientSecret.Should().Be(string.Empty);
    }
}
