using GauntletAI.AgentForge.Integration.OpenEmr;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Api.Health;

/// <summary>
/// Readiness check for the OpenEMR FHIR dependency (NFR-HEALTH-1): a real GET against the FHIR
/// capability statement - publicly readable per the FHIR spec, no bearer token needed - not an
/// unconditional pass.
/// </summary>
public sealed class OpenEmrHealthCheck(HttpClient httpClient, IOptions<OpenEmrOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var uri = $"{options.Value.BaseUrl.TrimEnd('/')}/apis/{options.Value.Site}/fhir/metadata";
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("OpenEMR FHIR capability statement reachable.")
                : HealthCheckResult.Unhealthy($"OpenEMR FHIR responded {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("OpenEMR FHIR unreachable.", ex);
        }
    }
}
