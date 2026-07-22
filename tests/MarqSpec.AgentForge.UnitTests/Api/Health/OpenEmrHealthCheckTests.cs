using System.Net;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Health;
using MarqSpec.AgentForge.Integration.OpenEmr;
using MarqSpec.AgentForge.UnitTests.TestSupport;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.UnitTests.Api.Health;

public sealed class OpenEmrHealthCheckTests
{
    private readonly OpenEmrOptions _options = new()
    {
        BaseUrl = "https://emr.example.org",
        Site = "default",
        ClientId = "client-1",
        Scopes = ["patient/patient.read"],
    };

    [Fact]
    public async Task CheckHealthAsync_CapabilityStatementReachable_ReturnsHealthy()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sut = new OpenEmrHealthCheck(new HttpClient(handler), Options.Create(_options));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://emr.example.org/apis/default/fhir/metadata");
    }

    [Fact]
    public async Task CheckHealthAsync_CapabilityStatementReturnsServerError_ReturnsUnhealthy()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var sut = new OpenEmrHealthCheck(new HttpClient(handler), Options.Create(_options));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_RequestThrows_ReturnsUnhealthyRatherThanPropagating()
    {
        // NFR-HEALTH-1: readiness must genuinely fail when the dependency is unreachable, not
        // return 200 unconditionally - and not crash the /ready endpoint either.
        var handler = new CapturingHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var sut = new OpenEmrHealthCheck(new HttpClient(handler), Options.Create(_options));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
