using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <summary>Structured log events for citation resolution. Counts and outcomes only — no patient ids or
/// document content (no PHI in logs, W2_ARCHITECTURE.md §12).</summary>
internal static partial class DocumentReferenceResolverLog
{
    [LoggerMessage(
        EventId = 4210,
        Level = LogLevel.Warning,
        Message = "No new DocumentReference appeared after the document write; leaving the citation pending.")]
    public static partial void NoNewReference(ILogger logger);

    [LoggerMessage(
        EventId = 4211,
        Level = LogLevel.Warning,
        Message = "Ambiguous citation resolution: {Count} new DocumentReferences appeared after one write; leaving pending.")]
    public static partial void AmbiguousReferences(ILogger logger, int count);

    [LoggerMessage(
        EventId = 4212,
        Level = LogLevel.Warning,
        Message = "DocumentReference lookup failed during citation resolution; leaving the citation pending.")]
    public static partial void LookupFailed(ILogger logger, Exception exception);
}
