using System.Net;
using FluentAssertions;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Epic 10's acceptance criterion: "/ready fails when any one dependency is down while /health
/// still succeeds" - proven against a real, deliberately unreachable OpenEMR (tests/AGENTS.md,
/// nothing mocked), not a fake health check result.
/// </summary>
public sealed class HealthEndpointReadinessTests : IClassFixture<HealthEndpointQaFixture>
{
    private readonly HealthEndpointQaFixture _fixture;

    public HealthEndpointReadinessTests(HealthEndpointQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetHealth_OpenEmrDependencyUnreachable_StillReturnsOkSinceItIsLivenessOnly()
    {
        // /health checks nothing but "is the process up" (Program.cs's Predicate = _ => false) -
        // it must not flap just because a downstream dependency happens to be unreachable.
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReady_OpenEmrDependencyUnreachable_ReturnsServiceUnavailable()
    {
        // PRD.md §13.1's "Dependency down" row: /ready must fail readiness so traffic isn't routed
        // to an instance that can't actually serve OpenEMR-backed requests.
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
