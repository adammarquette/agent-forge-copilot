using GauntletAI.AgentForge.Llm;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Api.Health;

/// <summary>
/// Readiness check for the LLM provider dependency (NFR-HEALTH-1). Deliberately unauthenticated -
/// this only needs to prove the network path is up, not exercise the real API key/quota - so any
/// HTTP response (including a 401) counts as reachable; only a transport-level failure means the
/// dependency itself is down.
/// </summary>
public sealed class LlmProviderHealthCheck(HttpClient httpClient, IOptions<LlmProviderOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(options.Value.BaseUrl, cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"LLM provider responded {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("LLM provider unreachable.", ex);
        }
    }
}
