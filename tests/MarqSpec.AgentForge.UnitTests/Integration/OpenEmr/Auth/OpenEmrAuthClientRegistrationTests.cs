using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Auth;

public sealed class OpenEmrAuthClientRegistrationTests
{
    [Fact]
    public async Task RegisterPublicClientAsync_ValidRequest_RegistersAsPublicClientWithPkceAuthMethod()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        var expected = new ClientRegistrationResponse("generated-client-id", null, "AgentForge Co-Pilot", ["https://sidecar.example.org/callback"]);
        ClientRegistrationRequest? captured = null;
        A.CallTo(() => api.RegisterClientAsync("default", A<ClientRegistrationRequest>._, A<CancellationToken>._))
            .Invokes((string _, ClientRegistrationRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(expected));
        var client = new OpenEmrAuthClient(api);

        var result = await client.RegisterPublicClientAsync(
            "default",
            "AgentForge Co-Pilot",
            ["https://sidecar.example.org/callback"],
            ["launch", "patient/patient.read", "openid", "fhirUser"],
            CancellationToken.None);

        result.Should().Be(expected);
        captured!.ClientName.Should().Be("AgentForge Co-Pilot");
        captured.RedirectUris.Should().Equal("https://sidecar.example.org/callback");
        captured.GrantTypes.Should().Equal("authorization_code", "refresh_token");
        captured.ResponseTypes.Should().Equal("code");
        captured.TokenEndpointAuthMethod.Should().Be("client_secret_post");
        captured.Scope.Should().Be("launch patient/patient.read openid fhirUser");
    }

    [Fact]
    public async Task RegisterPublicClientAsync_Always_PassesCancellationTokenThrough()
    {
        var api = A.Fake<IOpenEmrAuthApi>();
        A.CallTo(() => api.RegisterClientAsync(A<string>._, A<ClientRegistrationRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new ClientRegistrationResponse("id", null, null, null)));
        var client = new OpenEmrAuthClient(api);
        using var cts = new CancellationTokenSource();

        await client.RegisterPublicClientAsync("default", "name", ["https://x/callback"], ["launch"], cts.Token);

        A.CallTo(() => api.RegisterClientAsync("default", A<ClientRegistrationRequest>._, cts.Token)).MustHaveHappenedOnceExactly();
    }
}
