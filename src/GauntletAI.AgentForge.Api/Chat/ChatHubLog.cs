using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>
/// Source-generated log messages for <see cref="ChatHub"/> (CA1848). Never a clinical value or
/// the message payload itself - message kind and delivery outcome only (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class ChatHubLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to deliver chat message kind={Kind} sequence={Sequence} to connection {ConnectionId} - " +
            "it remains in the outbox for the next Resume call")]
    public static partial void MessageDeliveryFailed(ILogger logger, string kind, long sequence, string connectionId, Exception exception);
}
