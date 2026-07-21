using System.Net;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Health;
using MarqSpec.AgentForge.UnitTests.TestSupport;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.UnitTests.Api.Health;

public sealed class ObservabilityHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_PrometheusHealthUrlNotConfigured_ReturnsDegradedRatherThanUnconditionalPass()
    {
        // NFR-REL-2's "meaningful check, not an unconditional 200" - self-hosted observability is
        // optional infra (docker-compose), so an unconfigured URL is a real, distinct state from
        // "checked and confirmed reachable," not silently treated as healthy.
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sut = new ObservabilityHealthCheck(new HttpClient(handler), Options.Create(new ObservabilityOptions()));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_PrometheusReachable_ReturnsHealthy()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var options = new ObservabilityOptions { PrometheusHealthUrl = "http://prometheus.local/-/healthy" };
        var sut = new ObservabilityHealthCheck(new HttpClient(handler), Options.Create(options));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        handler.LastRequest!.RequestUri!.ToString().Should().Be("http://prometheus.local/-/healthy");
    }

    [Fact]
    public async Task CheckHealthAsync_PrometheusUnreachable_ReturnsUnhealthyRatherThanPropagating()
    {
        var handler = new CapturingHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var options = new ObservabilityOptions { PrometheusHealthUrl = "http://prometheus.local/-/healthy" };
        var sut = new ObservabilityHealthCheck(new HttpClient(handler), Options.Create(options));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
