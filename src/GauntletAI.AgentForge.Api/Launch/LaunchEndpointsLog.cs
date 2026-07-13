using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// Source-generated log messages for <see cref="LaunchEndpoints"/> (CA1848). Temporary diagnostics
/// for the "No pending SMART launch" callback failure (reference: gitlab#67): host, opaque session
/// id, and cookie names - none of which are PHI (ENGINEERING_STANDARDS.md §7). Remove once resolved.
/// </summary>
internal static partial class LaunchEndpointsLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SMART launch diag: host={Host} sessionId={SessionId}")]
    public static partial void LaunchDiag(ILogger logger, string host, string sessionId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SMART callback diag: host={Host} sessionId={SessionId} hasSessionCookie={HasSessionCookie} cookies=[{CookieNames}] pendingFound={PendingFound}")]
    public static partial void CallbackDiag(
        ILogger logger, string host, string sessionId, bool hasSessionCookie, string cookieNames, bool pendingFound);
}
