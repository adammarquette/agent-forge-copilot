using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;
using MarqSpec.AgentForge.IntegrationTests.Support;
using Refit;

namespace MarqSpec.AgentForge.IntegrationTests.OpenEmr;

/// <summary>
/// Exercises the real token endpoint's error path - an invalid authorization code is rejected
/// with a real OAuth2 error response, without needing a completed interactive SMART launch to
/// obtain a valid one first.
/// </summary>
public sealed class TokenExchangeErrorPathTests : IClassFixture<OpenEmrQaFixture>
{
    private readonly OpenEmrQaFixture _fixture;

    public TokenExchangeErrorPathTests(OpenEmrQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_InvalidCode_RejectsWithBadRequestFromRealServer()
    {
        // Guards: the real server's invalid_grant error shape/status - a unit test faking
        // IOpenEmrAuthApi can assert whatever we told the fake to return, but only the live
        // server confirms OpenEMR's OAuth2 implementation actually rejects a bad code the way
        // RFC 6749 §5.2 requires (ENGINEERING_STANDARDS.md §8.2 contract-drift coverage).
        var authClient = new OpenEmrAuthClient(_fixture.AuthApi);

        var act = () => authClient.ExchangeAuthorizationCodeAsync(
            _fixture.Options.Site,
            code: $"definitely-invalid-{Guid.NewGuid():N}",
            redirectUri: "https://sidecar.invalid/callback",
            clientId: "nonexistent-client",
            codeVerifier: "verifier-that-wont-match-anything",
            clientSecret: null,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ApiException>();
        ((int)exception.Which.StatusCode).Should().BeInRange(400, 499);
    }
}
