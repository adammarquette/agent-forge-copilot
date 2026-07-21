using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.Api.Health;

/// <summary>
/// Readiness check for the self-hosted observability backend (NFR-HEALTH-1): a real GET against
/// Prometheus's own <c>/-/healthy</c> endpoint when <see cref="ObservabilityOptions.PrometheusHealthUrl"/>
/// is configured. Unlike <see cref="OpenEmrHealthCheck"/>/<see cref="LlmProviderHealthCheck"/>,
/// this dependency is optional self-hosted infra (<c>observability/docker-compose.yml</c>), not
/// product-blocking - an unconfigured URL is reported <see cref="HealthStatus.Degraded"/>, a
/// distinct, honest state from "checked and confirmed reachable" (NFR-REL-2).
/// </summary>
public sealed class ObservabilityHealthCheck(HttpClient httpClient, IOptions<ObservabilityOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var url = options.Value.PrometheusHealthUrl;
        if (string.IsNullOrEmpty(url))
        {
            return HealthCheckResult.Degraded(
                $"{ObservabilityOptions.SectionName}:PrometheusHealthUrl not configured - metrics are emitted but scrape reachability is unverified.");
        }

        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Prometheus reachable.")
                : HealthCheckResult.Unhealthy($"Prometheus responded {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Prometheus unreachable.", ex);
        }
    }
}
