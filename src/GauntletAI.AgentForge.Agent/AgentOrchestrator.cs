using System.Diagnostics;
using System.Text.Json;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Observability;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// The multi-turn agent loop (ARCHITECTURE.md §8, D9). Turn 1 plans a bounded set of parallel
/// tool calls for the initial brief; follow-up turns maintain history so the model can resolve
/// references ("her", "that lab") and chain tools when one result is needed before the next call.
/// Grounding discipline and refusal boundaries live in <see cref="CardiologyProfile"/> (the
/// prompt); patient-scoping enforcement lives in <see cref="McpToolCatalog"/> and the tool
/// dispatcher (below the model). Every LLM-synthesized draft answer passes through
/// <see cref="IClinicalResponseVerifier"/> before it becomes an <see cref="AgentTurnResult"/> -
/// the mandatory gate FR-VERIF-0 requires. The one deliberate exception is the deterministic
/// fallback path (PRD.md §13.1, Epic 10): when the LLM call itself fails after retries exhausted,
/// the turn exceeds its tool-call round budget, or a malformed final answer is still malformed
/// after one repair attempt (below), the result is raw tool JSON, not synthesized prose - there is
/// no claim to ground, so it bypasses the verifier by design rather than being stripped to nothing
/// by a citation check built for prose. A malformed final answer (empty, truncated at MaxTokens, or
/// an unexpected stop) is rejected against the final-answer contract *before* the verifier and gets
/// one bounded repair re-prompt; only a well-formed answer reaches FR-VERIF-0.
/// </summary>
public sealed class AgentOrchestrator(
    ILlmProvider llmProvider,
    IMcpToolDispatcher toolDispatcher,
    IClinicalResponseVerifier verifier,
    IAgentForgeMetrics metrics,
    IOptions<AgentOptions> agentOptions,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    /// <summary>
    /// Safety bound on tool-call rounds within a single turn against a misbehaving model that
    /// never stops calling tools. Exceeding this degrades to the deterministic fallback (below)
    /// rather than looping indefinitely.
    /// </summary>
    private const int MaxToolCallRounds = 5;

    private const string BriefRequestPrompt =
        "Give me the pre-visit brief for this patient: what changed since the last visit and what matters today.";

    private const string AgendaSummaryPrompt =
        "Give me a short summary of this patient for today's agenda list - the one or two things " +
        "that matter most, in 1-3 sentences. This is read in a scan alongside several other " +
        "patients, not the full pre-visit brief.";

    private const string MalformedOutputRepairPrompt =
        "Your previous reply could not be used - it was empty or stopped before finishing. Reply " +
        "again with a complete, plain-text answer, citing each clinical fact with its " +
        "[ResourceType/Id] source exactly as instructed.";

    // Human-readable status for each tool, shown while the turn runs (gitlab#126). Perceived-latency only -
    // never the answer, which is verified before delivery.
    private static readonly Dictionary<string, string> ToolStatusLabels = new(StringComparer.Ordinal)
    {
        ["get_patient_summary"] = "the chart",
        ["get_interval_changes"] = "what changed since the last visit",
        ["get_labs"] = "labs",
        ["get_vitals"] = "vitals",
        ["get_recent_encounters"] = "recent visits",
        ["get_documents"] = "documents",
        ["get_document_facts"] = "the uploaded lab report",
        ["retrieve_evidence"] = "the guidelines",
    };

    private static string DescribeToolBatch(IReadOnlyList<LlmToolCall> toolCalls)
    {
        var labels = toolCalls
            .Select(call => ToolStatusLabels.TryGetValue(call.ToolName, out var label) ? label : call.ToolName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return labels.Length == 0 ? "Retrieving the record…" : "Reading " + string.Join(", ", labels) + "…";
    }

    /// <summary>Starts a new session for a patient and generates the initial pre-visit brief (UC-1).</summary>
    public Task<AgentTurnResult> StartBriefAsync(
        string site, string patientId, CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        var state = ConversationState.Start(site, patientId) with
        {
            Messages = [LlmMessage.FromText(LlmRole.User, BriefRequestPrompt)],
        };

        return RunTurnAsync(state, cancellationToken, progress);
    }

    /// <summary>Starts a new session for a patient and generates a short Daily Agenda summary (UC-6).</summary>
    public Task<AgentTurnResult> StartAgendaSummaryAsync(string site, string patientId, CancellationToken cancellationToken)
    {
        var state = ConversationState.Start(site, patientId) with
        {
            Messages = [LlmMessage.FromText(LlmRole.User, AgendaSummaryPrompt)],
        };

        return RunTurnAsync(state, cancellationToken);
    }

    /// <summary>Asks a follow-up question within an existing session, maintaining context (UC-2).</summary>
    public Task<AgentTurnResult> AskFollowUpAsync(
        ConversationState state, string question, CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        var updated = state with { Messages = [.. state.Messages, LlmMessage.FromText(LlmRole.User, question)] };
        return RunTurnAsync(updated, cancellationToken, progress);
    }

    private async Task<AgentTurnResult> RunTurnAsync(
        ConversationState state, CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        using var activity = AgentForgeActivitySource.Instance.StartActivity("agent.turn");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await RunTurnCoreAsync(state, progress, cancellationToken).ConfigureAwait(false);
            metrics.RecordAgentTurn(succeeded: true, stopwatch.Elapsed);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.RecordAgentTurn(succeeded: false, stopwatch.Elapsed);
            throw;
        }
    }

    private async Task<AgentTurnResult> RunTurnCoreAsync(
        ConversationState state, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Reviewing this patient's chart…");
        var messages = state.Messages;
        List<string> toolResultJsonThisTurn = [];

        // Per-encounter telemetry accumulators (FR-OBS-W2-1): assembled across the turn's rounds and emitted on
        // one correlation-scoped line at the verified final answer. reference: gitlab#135.
        var turnStopwatch = Stopwatch.StartNew();
        List<string> toolSequence = [];
        List<string> toolLatencies = [];
        long inputTokens = 0, outputTokens = 0;
        decimal costUsd = 0m;
        int? retrievalHits = null;
        List<double> extractionConfidences = [];

        // PRD.md §13.1's "per-request deadline" design default (the "tool slow / hits deadline"
        // row): bounds the whole turn's wall-clock time, not just each individual HTTP call (those
        // already have their own Polly attempt-timeout). deadlineOnlyCts is kept separate from the
        // linked token so the catch clauses below can tell "my deadline fired" apart from "the
        // caller cancelled" - the latter must still propagate, not degrade to a fallback.
        using var deadlineOnlyCts = new CancellationTokenSource(agentOptions.Value.TurnDeadline);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineOnlyCts.Token);
        var operationToken = linkedCts.Token;

        // PRD.md §13.1 "unexpected / unparseable model output": one bounded repair attempt across the
        // whole turn before degrading. Separate from the tool-call round budget below.
        var repairAttempted = false;

        for (var round = 0; round < MaxToolCallRounds; round++)
        {
            if (round > 0)
            {
                progress?.Report("Composing the answer…");
            }

            using var llmActivity = AgentForgeActivitySource.Instance.StartActivity("llm.complete");
            var request = new LlmRequest(CardiologyProfile.SystemPrompt, messages, McpToolCatalog.AllTools);

            LlmResponse response;
            try
            {
                response = await llmProvider.CompleteAsync(request, operationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadlineOnlyCts.IsCancellationRequested)
            {
                return BuildDeterministicFallback(state, messages, toolResultJsonThisTurn, "Turn exceeded its configured deadline.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // PRD.md §13.1: the HttpClient-level resilience pipeline (Polly, Epic 10) has
                // already retried transient failures before this exception ever reaches here -
                // this is the exhausted, final failure, so degrade rather than propagate. Whatever
                // detail ex.Message carries (AnthropicLlmProvider enriches this with the provider's
                // actual error body, not just the bare status line) flows straight into the log.
                return BuildDeterministicFallback(state, messages, toolResultJsonThisTurn, ex.Message);
            }

            metrics.RecordLlmUsage(response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.EstimatedCostUsd);
            inputTokens += response.Usage.InputTokens;
            outputTokens += response.Usage.OutputTokens;
            costUsd += response.Usage.EstimatedCostUsd;

            if (response.StopReason != LlmStopReason.ToolUse || response.ToolCalls.Count == 0)
            {
                // PRD.md §13.1: validate the model's final output against the "complete, non-empty
                // answer" contract (NFR-CONTRACT-1) *before* the verifier - an empty, truncated
                // (MaxTokens), or otherwise unexpected stop is rejected here, one repair re-prompt is
                // issued, and only then does it degrade to the deterministic fallback. This is
                // distinct from FR-VERIF-0's grounding gate, which runs on a well-formed answer below
                // and may still legitimately suppress it to nothing.
                if (!IsWellFormedFinalAnswer(response))
                {
                    if (!repairAttempted)
                    {
                        repairAttempted = true;
                        AgentOrchestratorLog.MalformedOutputRepairAttempt(
                            logger, response.StopReason, !string.IsNullOrWhiteSpace(response.Content));
                        messages =
                        [
                            .. messages,
                            BuildRejectedAssistantTurn(response),
                            LlmMessage.FromText(LlmRole.User, MalformedOutputRepairPrompt),
                        ];
                        continue;
                    }

                    return BuildDeterministicFallback(
                        state, messages, toolResultJsonThisTurn,
                        $"Model output failed the final-answer contract (stop reason {response.StopReason}) after one repair attempt.");
                }

                var verification = verifier.Verify(response.Content, toolResultJsonThisTurn);
                metrics.RecordVerificationResult(verification.Passed);

                // Guard the string building (CA1873): the per-encounter telemetry is Information level, so
                // build the joined fields into locals only when that level is enabled.
                if (logger.IsEnabled(LogLevel.Information))
                {
                    var toolSequenceText = string.Join(",", toolSequence);
                    var toolLatencyText = string.Join(",", toolLatencies);
                    double? confidence = extractionConfidences.Count > 0 ? extractionConfidences.Average() : null;
                    AgentOrchestratorLog.EncounterTelemetry(
                        logger,
                        toolSequenceText,
                        round + 1,
                        turnStopwatch.ElapsedMilliseconds,
                        toolLatencyText,
                        inputTokens,
                        outputTokens,
                        (double)costUsd,
                        retrievalHits,
                        confidence,
                        verification.Passed,
                        verification.SuppressedClaims.Count);
                }

                var verifiedMessages = messages.Append(
                    new LlmMessage(LlmRole.Assistant, [new LlmTextContent(verification.VerifiedAnswer)]));

                return new AgentTurnResult(
                    verification.VerifiedAnswer,
                    state with { Messages = [.. verifiedMessages] },
                    verification.ConstraintFlags,
                    verification.SuppressedClaims);
            }

            messages = [.. messages, new LlmMessage(LlmRole.Assistant, BuildAssistantContent(response))];

            progress?.Report(DescribeToolBatch(response.ToolCalls));

            (LlmToolResultContent Result, string Tool, double Ms)[] dispatched;
            try
            {
                dispatched = await Task.WhenAll(
                    response.ToolCalls.Select(call => TimedDispatchAsync(state.Site, state.PatientId, call, operationToken)))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadlineOnlyCts.IsCancellationRequested)
            {
                return BuildDeterministicFallback(state, messages, toolResultJsonThisTurn, "Turn exceeded its configured deadline.");
            }

            var toolResults = new LlmToolResultContent[dispatched.Length];
            for (var i = 0; i < dispatched.Length; i++)
            {
                var d = dispatched[i];
                toolResults[i] = d.Result;
                toolSequence.Add(d.Tool);
                toolLatencies.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{d.Tool}={d.Ms:F0}"));
                AccumulateToolTelemetry(d.Tool, d.Result.ResultJson, ref retrievalHits, extractionConfidences);
            }

            toolResultJsonThisTurn.AddRange(toolResults.Select(r => r.ResultJson));
            messages = [.. messages, new LlmMessage(LlmRole.User, toolResults)];
        }

        return BuildDeterministicFallback(
            state, messages, toolResultJsonThisTurn, $"Exceeded {MaxToolCallRounds} tool-call rounds without a final answer.");
    }

    /// <summary>
    /// Builds the PRD.md §13.1 "deterministic, non-LLM fallback" - raw source data pulled straight
    /// from whatever tools already succeeded this turn, no synthesis, never silent. Bypasses
    /// <see cref="IClinicalResponseVerifier"/> deliberately (see class doc comment).
    /// </summary>
    private AgentTurnResult BuildDeterministicFallback(
        ConversationState state, IReadOnlyList<LlmMessage> messages, List<string> toolResultJsonThisTurn, string reason)
    {
        AgentOrchestratorLog.DegradedToDeterministicFallback(logger, reason);

        var answer = toolResultJsonThisTurn.Count == 0
            ? "Summary unavailable right now, and no data could be retrieved this turn either. Please try again."
            : "Summary unavailable right now - here is the source data retrieved this turn instead:\n\n" +
              string.Join("\n\n", toolResultJsonThisTurn.Select((json, index) => $"Source {index + 1}: {json}"));

        var updatedMessages = messages.Append(new LlmMessage(LlmRole.Assistant, [new LlmTextContent(answer)]));

        return new AgentTurnResult(answer, state with { Messages = [.. updatedMessages] }, [], [], IsDeterministicFallback: true);
    }

    private async Task<LlmToolResultContent> DispatchAndLogAsync(
        string site, string patientId, LlmToolCall toolCall, CancellationToken cancellationToken)
    {
        var result = await toolDispatcher.DispatchAsync(site, patientId, toolCall, cancellationToken).ConfigureAwait(false);
        AgentOrchestratorLog.ToolCallDispatched(logger, toolCall.ToolName, result.IsError);
        return result;
    }

    // Times a single tool dispatch so the per-encounter telemetry (FR-OBS-W2-1) can report latency by step.
    private async Task<(LlmToolResultContent Result, string Tool, double Ms)> TimedDispatchAsync(
        string site, string patientId, LlmToolCall toolCall, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await DispatchAndLogAsync(site, patientId, toolCall, cancellationToken).ConfigureAwait(false);
        return (result, toolCall.ToolName, stopwatch.Elapsed.TotalMilliseconds);
    }

    // Pulls the two per-encounter signals that live inside tool results: retrieval hit count from
    // retrieve_evidence ({"Snippets":[...]}) and extraction confidence from get_document_facts
    // ({"Facts":[{"Confidence":x},...]}). Tolerant: an error/malformed result simply leaves the signal absent.
    private static void AccumulateToolTelemetry(
        string tool, string resultJson, ref int? retrievalHits, List<double> extractionConfidences)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (tool == "retrieve_evidence"
                && root.TryGetProperty("Snippets", out var snippets) && snippets.ValueKind == JsonValueKind.Array)
            {
                retrievalHits = (retrievalHits ?? 0) + snippets.GetArrayLength();
            }
            else if (tool == "get_document_facts"
                && root.TryGetProperty("Facts", out var facts) && facts.ValueKind == JsonValueKind.Array)
            {
                foreach (var fact in facts.EnumerateArray())
                {
                    if (fact.TryGetProperty("Confidence", out var c) && c.ValueKind == JsonValueKind.Number)
                    {
                        extractionConfidences.Add(c.GetDouble());
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Telemetry is best-effort: a malformed/error tool result never breaks the turn.
        }
    }

    /// <summary>
    /// The final-answer contract (PRD.md §13.1 / NFR-CONTRACT-1): a usable answer is one the model
    /// finished cleanly (<see cref="LlmStopReason.EndTurn"/>) with actual text. A truncated
    /// (<see cref="LlmStopReason.MaxTokens"/>), unexpected (<see cref="LlmStopReason.Other"/>), or
    /// empty response is malformed - it may cut off mid-fact or mid-citation, so it is not shipped.
    /// </summary>
    private static bool IsWellFormedFinalAnswer(LlmResponse response) =>
        response.StopReason == LlmStopReason.EndTurn && !string.IsNullOrWhiteSpace(response.Content);

    /// <summary>
    /// Appends the model's own rejected turn before the repair prompt so the re-prompt has an
    /// assistant turn to answer, preserving the alternating user/assistant shape real providers
    /// require. Empty/whitespace content is replaced with a marker so the message is never itself
    /// empty (which providers reject).
    /// </summary>
    private static LlmMessage BuildRejectedAssistantTurn(LlmResponse response)
    {
        var text = string.IsNullOrWhiteSpace(response.Content) ? "(no answer produced)" : response.Content;
        return new LlmMessage(LlmRole.Assistant, [new LlmTextContent(text)]);
    }

    private static List<LlmContent> BuildAssistantContent(LlmResponse response)
    {
        List<LlmContent> content = [];
        if (!string.IsNullOrEmpty(response.Content))
        {
            content.Add(new LlmTextContent(response.Content));
        }

        content.AddRange(response.ToolCalls.Select(c => new LlmToolUseContent(c.Id, c.ToolName, c.ArgumentsJson)));

        return content;
    }
}
