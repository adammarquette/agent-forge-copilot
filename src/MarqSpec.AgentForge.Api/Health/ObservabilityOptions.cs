namespace MarqSpec.AgentForge.Api.Health;

/// <summary>
/// Self-hosted observability backend configuration (Epic 9 - docker-compose Prometheus/Grafana),
/// bound via the Options pattern. Distinct from <see cref="Integration.OpenEmr.OpenEmrOptions"/>/
/// <see cref="Llm.LlmProviderOptions"/> in one way: this dependency is optional infra, not a
/// product-blocking one, so nothing here is <c>required</c> or validated on start.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "Observability";

    /// <summary>
    /// The self-hosted Prometheus instance's <c>/-/healthy</c> endpoint, if deployed
    /// (<c>observability/docker-compose.yml</c>). Optional - <see cref="ObservabilityHealthCheck"/>
    /// reports <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded"/>
    /// rather than an unconditional pass when it's unset (NFR-REL-2).
    /// </summary>
    public string? PrometheusHealthUrl { get; init; }

    /// <summary>
    /// Full OTLP/HTTP logs endpoint of a self-hosted Loki instance (Epic 107), e.g.
    /// <c>http://agentforge-loki.railway.internal:3100/otlp/v1/logs</c>. When set, the sidecar adds an
    /// OTLP log exporter alongside the console one (<c>Program.cs</c>) so structured logs are queryable
    /// in Grafana. Optional and fail-open: unset -> console-only logging, and a wrong/unreachable value
    /// never blocks the app (the exporter batches and drops on failure). Include the full
    /// <c>/otlp/v1/logs</c> path - it is used as-is, not appended to. reference: gitlab#107
    /// </summary>
    public string? LokiOtlpEndpoint { get; init; }
}
