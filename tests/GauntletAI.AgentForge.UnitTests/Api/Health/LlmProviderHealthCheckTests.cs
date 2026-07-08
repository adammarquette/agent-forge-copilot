using System.Net;
using FluentAssertions;
using GauntletAI.AgentForge.Api.Health;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.UnitTests.TestSupport;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.UnitTests.Api.Health;

public sealed class LlmProviderHealthCheckTests
{
    private readonly LlmProviderOptions _options = new()
    {
        ApiKey = "test-key",
        Model = "test-model",
        BaseUrl = "https://api.anthropic.example",
        InputPricePerMillionTokensUsd = 0,
        OutputPricePerMillionTokensUsd = 0,
    };

    [Fact]
    public async Task CheckHealthAsync_ProviderResponds_ReturnsHealthyEvenOnAnUnauthorizedStatus()
    {
        // Any HTTP response proves the network path is up; the request carries no API key, so a
        // 401 here is expected and still proves the dependency itself is reachable.
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = new LlmProviderHealthCheck(new HttpClient(handler), Options.Create(_options));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_RequestThrows_ReturnsUnhealthyRatherThanPropagating()
    {
        var handler = new CapturingHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var sut = new LlmProviderHealthCheck(new HttpClient(handler), Options.Create(_options));

        var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
