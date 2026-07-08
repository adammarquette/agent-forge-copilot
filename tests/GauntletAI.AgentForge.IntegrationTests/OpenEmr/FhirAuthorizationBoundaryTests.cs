using FluentAssertions;
using GauntletAI.AgentForge.IntegrationTests.Support;
using Refit;

namespace GauntletAI.AgentForge.IntegrationTests.OpenEmr;

/// <summary>
/// Confirms the real FHIR API enforces authentication at the boundary - a request with no bearer
/// token is rejected, not silently served. This is the lowest layer of FR-AUTH-1 ("an
/// unauthenticated request is rejected before any tool runs"); the tool/orchestrator layers
/// (later epics) add role/relationship checks on top, but the FHIR server itself refusing an
/// unauthenticated read is the foundation everything else stands on.
/// </summary>
public sealed class FhirAuthorizationBoundaryTests : IClassFixture<OpenEmrQaFixture>
{
    private readonly OpenEmrQaFixture _fixture;

    public FhirAuthorizationBoundaryTests(OpenEmrQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReadAsync_NoBearerToken_RejectsWithUnauthorizedFromRealServer()
    {
        var act = () => _fixture.UnauthenticatedFhirApi.ReadAsync(
            _fixture.Options.Site, "Patient", "1", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ApiException>();
        ((int)exception.Which.StatusCode).Should().BeOneOf(401, 403);
    }

    [Fact]
    public async Task SearchConditionsAsync_NoBearerToken_RejectsWithUnauthorizedFromRealServer()
    {
        var act = () => _fixture.UnauthenticatedFhirApi.SearchConditionsAsync(
            _fixture.Options.Site, "1", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ApiException>();
        ((int)exception.Which.StatusCode).Should().BeOneOf(401, 403);
    }
}
