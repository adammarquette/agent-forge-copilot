using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Agent;

/// <summary>Source-generated log messages for <see cref="McpToolDispatcher"/> (CA1848).</summary>
internal static partial class McpToolDispatcherLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Suspicious tool call: {ToolName} arguments carried an unexpected '{FieldName}' field - " +
            "ignored and the session-bound site/patientId used instead (FR-AUTH-3)")]
    public static partial void SuspiciousArgumentOverrideAttempt(ILogger logger, string toolName, string fieldName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "get_document_facts was called but no document-facts source is wired; returning no facts")]
    public static partial void DocumentFactsToolUnavailable(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "retrieve_evidence was called but no evidence retriever is wired; returning no snippets")]
    public static partial void EvidenceToolUnavailable(ILogger logger);
}
