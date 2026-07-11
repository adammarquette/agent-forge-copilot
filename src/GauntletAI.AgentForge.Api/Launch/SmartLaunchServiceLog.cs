using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// Source-generated log messages for <see cref="SmartLaunchService"/> (CA1848). Introspection
/// metadata only - never a token, patient identifier, or other PHI (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class SmartLaunchServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Introspection returned no subject claim for client {ClientId}: active={Active}")]
    public static partial void IntrospectionMissingSubject(ILogger logger, string? clientId, bool active);
}
