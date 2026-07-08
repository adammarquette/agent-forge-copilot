using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// Source-generated audit log messages for <see cref="McpToolServer"/> (CA1848 - LoggerMessage
/// delegates rather than direct ILogger calls). Every message carries the correlation id and
/// result counts only - never a patient id or clinical value (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class McpToolServerLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "MCP tool {ToolName} completed for correlation {CorrelationId}: patient found={PatientFound}, " +
            "{ProblemCount} problems, {MedicationCount} medications, {AllergyCount} allergies")]
    public static partial void PatientSummaryCompleted(
        ILogger logger,
        string toolName,
        string correlationId,
        bool patientFound,
        int problemCount,
        int medicationCount,
        int allergyCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "MCP tool {ToolName} completed for correlation {CorrelationId}: {ResultCount} results")]
    public static partial void ResultCountCompleted(
        ILogger logger, string toolName, string correlationId, int resultCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "MCP tool {ToolName} completed for correlation {CorrelationId}: " +
            "{MedicationChangeCount} medication changes, {NewLabCount} new labs, {EncounterCount} interval encounters")]
    public static partial void IntervalChangesCompleted(
        ILogger logger,
        string toolName,
        string correlationId,
        int medicationChangeCount,
        int newLabCount,
        int encounterCount);
}
