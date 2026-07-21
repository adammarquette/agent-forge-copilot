using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;
using MarqSpec.AgentForge.IntegrationTests.Support;
using Refit;

namespace MarqSpec.AgentForge.IntegrationTests.OpenEmr;

/// <summary>
/// Exercises the real introspection endpoint with a token that cannot possibly be valid. RFC 7662
/// §2.2 requires the server to respond with <c>active: false</c> rather than an error for an
/// invalid/expired/revoked token - confirmed against this real QA server, it does NOT comply: a
/// non-JWT-shaped token gets an HTTP 400 ("The JWT string must have two dots") and a
/// JWT-shaped-but-garbage token gets an HTTP 500 with an empty body (an unhandled server error, not
/// a graceful refusal). Since neither shape is fixable from our side, this asserts the weaker but
/// actually-load-bearing property instead: whatever OpenEMR does, it must never affirm a
/// never-issued token as active (FR-AUTH-4 depends on this, not on RFC-7662-exact response shape).
/// </summary>
public sealed class TokenIntrospectionTests : IClassFixture<OpenEmrQaFixture>
{
    private readonly OpenEmrQaFixture _fixture;

    public TokenIntrospectionTests(OpenEmrQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IntrospectAsync_TokenThatWasNeverIssued_NeverReportsItActive()
    {
        // OpenEMR's /introspect requires the caller to authenticate as a registered client via
        // client_id/client_secret form parameters (confirmed against this server - HTTP Basic Auth
        // is not accepted). A freshly self-registered client can't be used here: OpenEMR leaves
        // dynamically-registered clients disabled until an admin approves them in the UI, so this
        // needs TestClientId/TestClientSecret - a client registered and enabled once out-of-band,
        // the same way TestAccessToken is.
        if (string.IsNullOrEmpty(_fixture.Options.TestClientId))
        {
            throw new InvalidOperationException(
                $"{QaOpenEmrOptions.SectionName}__TestClientId (and TestClientSecret) must be set to an " +
                "enabled OpenEMR client - see QaOpenEmrOptions.TestClientId for why this can't be " +
                "self-registered on the fly.");
        }

        var authClient = new OpenEmrAuthClient(_fixture.AuthApi);

        IntrospectionResponse? response;
        try
        {
            response = await authClient.IntrospectAsync(
                _fixture.Options.Site,
                $"never-issued-{Guid.NewGuid():N}",
                _fixture.Options.TestClientId,
                _fixture.Options.TestClientSecret,
                CancellationToken.None);
        }
        catch (ApiException)
        {
            // The server refused the call outright instead of returning active:false - not
            // RFC-7662-compliant, but it never affirmed the token as active either, so the
            // property this test actually guards still holds. See the class doc comment.
            return;
        }

        response.Active.Should().BeFalse();
    }
}
