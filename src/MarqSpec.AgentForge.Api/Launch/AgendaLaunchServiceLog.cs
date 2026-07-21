using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.Api.Launch;

/// <summary>
/// Source-generated log messages for <see cref="AgendaLaunchService"/> (CA1848). Introspection
/// metadata only - never a token, patient identifier, or other PHI (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class AgendaLaunchServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Introspection returned no subject claim for client {ClientId}: active={Active}")]
    public static partial void IntrospectionMissingSubject(ILogger logger, string? clientId, bool active);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Introspection reports the token is not active for client {ClientId}")]
    public static partial void IntrospectionInactive(ILogger logger, string? clientId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Agenda (main-tab) launch token carried an unexpected launch patient context - the fork's INTENT_MAIN_TAB launch may not have behaved as expected")]
    public static partial void UnexpectedPatientContext(ILogger logger);
}
