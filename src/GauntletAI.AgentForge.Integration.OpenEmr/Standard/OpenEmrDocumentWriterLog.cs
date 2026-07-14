using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <summary>Structured log events for the document writer. Status codes only — never patient ids, file
/// names, or content (no PHI in logs, W2_ARCHITECTURE.md §12).</summary>
internal static partial class OpenEmrDocumentWriterLog
{
    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Warning,
        Message = "OpenEMR document write rejected for auth reasons (status {StatusCode}); check the admin api:oemr scope + patients:docs ACL.")]
    public static partial void Unauthorized(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Warning,
        Message = "No administrative access token available for the OpenEMR document write.")]
    public static partial void NoAdminToken(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 4202,
        Level = LogLevel.Warning,
        Message = "OpenEMR document write failed with a non-success status ({StatusCode}).")]
    public static partial void FailedStatus(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 4203,
        Level = LogLevel.Warning,
        Message = "OpenEMR document write failed after resilience.")]
    public static partial void FailedException(ILogger logger, Exception exception);
}
