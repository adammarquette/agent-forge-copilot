using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>Source-generated structured logging for the retrieval tier. No query text or PHI is logged.</summary>
internal static partial class RetrievalLog
{
    [LoggerMessage(EventId = 5201, Level = LogLevel.Warning,
        Message = "Hybrid retrieval {Half} half failed; degrading and continuing without it.")]
    public static partial void HalfDegraded(ILogger logger, string half, Exception exception);

    [LoggerMessage(EventId = 5202, Level = LogLevel.Warning,
        Message = "Reranker unavailable; returning the RRF-fused order.")]
    public static partial void RerankDegraded(ILogger logger, Exception exception);
}
