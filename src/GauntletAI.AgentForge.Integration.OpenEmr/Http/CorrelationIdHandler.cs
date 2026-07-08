namespace GauntletAI.AgentForge.Integration.OpenEmr.Http;

/// <summary>
/// Propagates the current request's correlation id onto every outbound OpenEMR call, so a full
/// trace is reconstructable from logs alone (FR-OBS-1, NFR-TRACE-1).
/// </summary>
public sealed class CorrelationIdHandler(ICorrelationIdAccessor correlationIdAccessor) : DelegatingHandler
{
    /// <summary>HTTP header the correlation id is propagated under.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.Add(HeaderName, correlationIdAccessor.CorrelationId);

        return base.SendAsync(request, cancellationToken);
    }
}
