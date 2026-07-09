using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using GauntletAI.AgentForge.IntegrationTests.Support;

namespace GauntletAI.AgentForge.IntegrationTests.OpenEmr;

/// <summary>
/// Exercises the real dynamic client registration endpoint (RFC 7591) against the QA OpenEMR
/// deployment. Registration needs no prior authentication, so this is genuinely runnable without
/// a pre-obtained token - unlike most of this OAuth surface.
/// </summary>
public sealed class DynamicClientRegistrationTests : IClassFixture<OpenEmrQaFixture>
{
    private readonly OpenEmrQaFixture _fixture;

    public DynamicClientRegistrationTests(OpenEmrQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RegisterPublicClientAsync_AgainstRealQaServer_ReturnsAssignedClientId()
    {
        // Guards: real dynamic client registration end-to-end, including whatever field
        // requirements/defaults the live authorization server enforces that a unit test faking
        // IOpenEmrAuthApi can't catch (contract drift - ENGINEERING_STANDARDS.md §8.2).
        var authClient = new OpenEmrAuthClient(_fixture.AuthApi);

        var response = await authClient.RegisterPublicClientAsync(
            _fixture.Options.Site,
            clientName: $"agentforge-integration-test-{Guid.NewGuid():N}",
            redirectUris: ["https://sidecar.invalid/callback"],
            scopes: ["launch", "patient/Patient.read", "openid", "fhirUser", "api:fhir"],
            CancellationToken.None);

        response.ClientId.Should().NotBeNullOrWhiteSpace();
    }
}
