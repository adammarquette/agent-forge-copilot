using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Observability;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// Executes a tool call the model requested against the real <see cref="IMcpToolServer"/>,
/// forcing <c>site</c>/<c>patientId</c> from the session context on every dispatch regardless of
/// what the call's arguments contain - the enforcement half of
/// FR-CHAT-3's patient-scoping (<see cref="McpToolCatalog"/> is the schema half). Every call
/// returns a result, never throws for an expected failure: a provider requires exactly one
/// tool_result per tool_use before the conversation can continue, so an unknown tool, malformed
/// arguments, or a downstream contract failure all become an error result the model can see and
/// react to, not a crash (NFR-REL-1 - one tool failing degrades one step, not the whole turn).
/// </summary>
public sealed class McpToolDispatcher(
    IMcpToolServer toolServer, IAgentForgeMetrics metrics, ILogger<McpToolDispatcher> logger) : IMcpToolDispatcher
{
    private static readonly JsonSerializerOptions ArgumentsJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions ResultJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Field names McpToolCatalog's schemas never offer the model - if one shows up in real
    /// arguments anyway, it is ignored (enforced elsewhere in this class) but the attempt itself
    /// is worth recording (FR-AUTH-3: "confirmed... and get logged," not just confirmed harmless).
    /// </summary>
    private static readonly string[] UnexpectedArgumentFields = ["patientid", "patient_id", "site"];

    /// <inheritdoc />
    public async Task<LlmToolResultContent> DispatchAsync(
        string site, string patientId, LlmToolCall toolCall, CancellationToken cancellationToken)
    {
        DetectSuspiciousArgumentOverride(toolCall);

        using var activity = AgentForgeActivitySource.Instance.StartActivity($"tool.{toolCall.ToolName}");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var resultJson = await ExecuteAsync(site, patientId, toolCall, cancellationToken).ConfigureAwait(false);
            metrics.RecordToolCall(toolCall.ToolName, succeeded: true, stopwatch.Elapsed);
            return new LlmToolResultContent(toolCall.Id, resultJson);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.RecordToolCall(toolCall.ToolName, succeeded: false, stopwatch.Elapsed);
            return new LlmToolResultContent(toolCall.Id, SerializeError(ex.Message), IsError: true);
        }
    }

    private void DetectSuspiciousArgumentOverride(LlmToolCall toolCall)
    {
        if (string.IsNullOrWhiteSpace(toolCall.ArgumentsJson))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(toolCall.ArgumentsJson);
        }
        catch (JsonException)
        {
            // Malformed JSON is handled as a contract failure by the normal dispatch path - nothing to detect here.
            return;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (UnexpectedArgumentFields.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    McpToolDispatcherLog.SuspiciousArgumentOverrideAttempt(logger, toolCall.ToolName, property.Name);
                }
            }
        }
    }

    private Task<string> ExecuteAsync(string site, string patientId, LlmToolCall call, CancellationToken cancellationToken) =>
        call.ToolName switch
        {
            "get_patient_summary" => ExecuteGetPatientSummaryAsync(site, patientId, cancellationToken),
            "get_interval_changes" => ExecuteGetIntervalChangesAsync(site, patientId, call.ArgumentsJson, cancellationToken),
            "get_labs" => ExecuteGetLabsAsync(site, patientId, call.ArgumentsJson, cancellationToken),
            "get_vitals" => ExecuteGetVitalsAsync(site, patientId, call.ArgumentsJson, cancellationToken),
            "get_recent_encounters" => ExecuteGetRecentEncountersAsync(site, patientId, call.ArgumentsJson, cancellationToken),
            "get_documents" => ExecuteGetDocumentsAsync(site, patientId, call.ArgumentsJson, cancellationToken),
            _ => throw new McpToolContractException(call.ToolName, [$"Unknown tool '{call.ToolName}'."]),
        };

    private async Task<string> ExecuteGetPatientSummaryAsync(string site, string patientId, CancellationToken cancellationToken)
    {
        var result = await toolServer.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = site, PatientId = patientId }, cancellationToken).ConfigureAwait(false);
        return Serialize(result);
    }

    private async Task<string> ExecuteGetIntervalChangesAsync(
        string site, string patientId, string argumentsJson, CancellationToken cancellationToken)
    {
        var args = ParseArguments<IntervalChangesArguments>(argumentsJson);
        var sinceDate = args?.SinceDate
            ?? throw new McpToolContractException("get_interval_changes", ["since_date is required."]);

        var result = await toolServer.GetIntervalChangesAsync(
            new GetIntervalChangesRequest { Site = site, PatientId = patientId, SinceDate = sinceDate }, cancellationToken)
            .ConfigureAwait(false);
        return Serialize(result);
    }

    private async Task<string> ExecuteGetLabsAsync(
        string site, string patientId, string argumentsJson, CancellationToken cancellationToken)
    {
        var args = ParseArguments<DateFilterArguments>(argumentsJson);
        var result = await toolServer.GetLabsAsync(
            new GetLabsRequest { Site = site, PatientId = patientId, SinceDate = args?.SinceDate }, cancellationToken)
            .ConfigureAwait(false);
        return Serialize(result);
    }

    private async Task<string> ExecuteGetVitalsAsync(
        string site, string patientId, string argumentsJson, CancellationToken cancellationToken)
    {
        var args = ParseArguments<DateFilterArguments>(argumentsJson);
        var result = await toolServer.GetVitalsAsync(
            new GetVitalsRequest { Site = site, PatientId = patientId, SinceDate = args?.SinceDate }, cancellationToken)
            .ConfigureAwait(false);
        return Serialize(result);
    }

    private async Task<string> ExecuteGetRecentEncountersAsync(
        string site, string patientId, string argumentsJson, CancellationToken cancellationToken)
    {
        var args = ParseArguments<RecentEncountersArguments>(argumentsJson);
        var request = new GetRecentEncountersRequest { Site = site, PatientId = patientId };
        if (args?.Count is { } count)
        {
            request = request with { Count = count };
        }

        var result = await toolServer.GetRecentEncountersAsync(request, cancellationToken).ConfigureAwait(false);
        return Serialize(result);
    }

    private async Task<string> ExecuteGetDocumentsAsync(
        string site, string patientId, string argumentsJson, CancellationToken cancellationToken)
    {
        var args = ParseArguments<DocumentsArguments>(argumentsJson);
        var result = await toolServer.GetDocumentsAsync(
            new GetDocumentsRequest { Site = site, PatientId = patientId, DocumentType = args?.DocumentType }, cancellationToken)
            .ConfigureAwait(false);
        return Serialize(result);
    }

    private static T? ParseArguments<T>(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson) || argumentsJson.Trim() == "{}")
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(argumentsJson, ArgumentsJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new McpToolContractException("(argument parsing)", [$"Arguments are not valid JSON: {ex.Message}"]);
        }
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, ResultJsonOptions);

    private static string SerializeError(string message) => JsonSerializer.Serialize(new { error = message });

    private sealed record IntervalChangesArguments([property: JsonPropertyName("since_date")] string? SinceDate);

    private sealed record DateFilterArguments([property: JsonPropertyName("since_date")] string? SinceDate);

    private sealed record RecentEncountersArguments([property: JsonPropertyName("count")] int? Count);

    private sealed record DocumentsArguments([property: JsonPropertyName("document_type")] string? DocumentType);
}
