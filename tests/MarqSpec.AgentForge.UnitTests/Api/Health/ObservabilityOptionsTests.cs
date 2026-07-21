using FluentAssertions;
using MarqSpec.AgentForge.Api.Health;
using Microsoft.Extensions.Configuration;

namespace MarqSpec.AgentForge.UnitTests.Api.Health;

public sealed class ObservabilityOptionsTests
{
    [Fact]
    public void Bind_LokiOtlpEndpointConfigured_IsReadFromObservabilitySection()
    {
        // The sidecar ships logs to a self-hosted Loki (Epic 107) only when an operator sets this
        // endpoint - so binding it from the "Observability" section is the config contract that turns
        // the OTLP log exporter on. reference: gitlab#107
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:LokiOtlpEndpoint"] = "http://loki:3100/otlp/v1/logs",
            })
            .Build();

        var options = config.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>();

        options!.LokiOtlpEndpoint.Should().Be("http://loki:3100/otlp/v1/logs");
    }

    [Fact]
    public void Bind_LokiOtlpEndpointAbsent_DefaultsToNullSoTheExporterStaysOff()
    {
        // Fail open: no endpoint configured -> null -> console-only logging, app still boots. Mirrors
        // PrometheusHealthUrl's optional-infra precedent (nothing here is required/validated on start).
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var options = config.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();

        options.LokiOtlpEndpoint.Should().BeNull();
    }
}
