using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using GauntletAI.AgentForge.IntegrationTests.Support;

namespace GauntletAI.AgentForge.IntegrationTests.OpenEmr;

/// <summary>
/// Exercises the real introspection endpoint (RFC 7662) with a token that cannot possibly be
/// valid - no prior authentication needed, since RFC 7662 requires introspecting an
/// invalid/expired/revoked token to return <c>active: false</c>, not an error.
/// </summary>
public sealed class TokenIntrospectionTests : IClassFixture<OpenEmrQaFixture>
{
    private readonly OpenEmrQaFixture _fixture;

    public TokenIntrospectionTests(OpenEmrQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IntrospectAsync_TokenThatWasNeverIssued_ReturnsActiveFalsePerRfc7662()
    {
        // Guards: RFC 7662 §2.2 compliance on the live server - "if the introspection call is
        // properly authorized but the token is not active... the authorization server MUST
        // instead respond with an introspection response with the "active" field set to "false"".
        // A unit test faking IOpenEmrAuthApi can't tell us whether OpenEMR actually does this.
        var authClient = new OpenEmrAuthClient(_fixture.AuthApi);

        var response = await authClient.IntrospectAsync(
            _fixture.Options.Site, $"never-issued-{Guid.NewGuid():N}", CancellationToken.None);

        response.Active.Should().BeFalse();
    }
}
