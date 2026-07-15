using GauntletAI.AgentForge.Llm;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// Source-generated log messages for <see cref="AgentOrchestrator"/> (CA1848). Tool names and
/// error flags only - never a clinical value or patient identifier (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class AgentOrchestratorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Tool call dispatched: {ToolName}, error={IsError}")]
    public static partial void ToolCallDispatched(ILogger logger, string toolName, bool isError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Degraded to deterministic fallback: {Reason}")]
    public static partial void DegradedToDeterministicFallback(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected malformed model output (stopReason={StopReason}, hasContent={HasContent}); attempting one repair.")]
    public static partial void MalformedOutputRepairAttempt(ILogger logger, LlmStopReason stopReason, bool hasContent);
}
