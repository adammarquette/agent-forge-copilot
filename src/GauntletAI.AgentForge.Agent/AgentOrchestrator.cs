using System.Diagnostics;
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
/// or the turn exceeds its tool-call round budget, the result is raw tool JSON, not synthesized
/// prose - there is no claim to ground, so it bypasses the verifier by design rather than being
/// stripped to nothing by a citation check built for prose.
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

    /// <summary>Starts a new session for a patient and generates the initial pre-visit brief (UC-1).</summary>
    public Task<AgentTurnResult> StartBriefAsync(string site, string patientId, CancellationToken cancellationToken)
    {
        var state = ConversationState.Start(site, patientId) with
        {
            Messages = [LlmMessage.FromText(LlmRole.User, BriefRequestPrompt)],
        };

        return RunTurnAsync(state, cancellationToken);
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
        ConversationState state, string question, CancellationToken cancellationToken)
    {
        var updated = state with { Messages = [.. state.Messages, LlmMessage.FromText(LlmRole.User, question)] };
        return RunTurnAsync(updated, cancellationToken);
    }

    private async Task<AgentTurnResult> RunTurnAsync(ConversationState state, CancellationToken cancellationToken)
    {
        using var activity = AgentForgeActivitySource.Instance.StartActivity("agent.turn");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await RunTurnCoreAsync(state, cancellationToken).ConfigureAwait(false);
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

    private async Task<AgentTurnResult> RunTurnCoreAsync(ConversationState state, CancellationToken cancellationToken)
    {
        var messages = state.Messages;
        List<string> toolResultJsonThisTurn = [];

        // PRD.md §13.1's "per-request deadline" design default (the "tool slow / hits deadline"
        // row): bounds the whole turn's wall-clock time, not just each individual HTTP call (those
        // already have their own Polly attempt-timeout). deadlineOnlyCts is kept separate from the
        // linked token so the catch clauses below can tell "my deadline fired" apart from "the
        // caller cancelled" - the latter must still propagate, not degrade to a fallback.
        using var deadlineOnlyCts = new CancellationTokenSource(agentOptions.Value.TurnDeadline);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineOnlyCts.Token);
        var operationToken = linkedCts.Token;

        for (var round = 0; round < MaxToolCallRounds; round++)
        {
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

            if (response.StopReason != LlmStopReason.ToolUse || response.ToolCalls.Count == 0)
            {
                var verification = verifier.Verify(response.Content, toolResultJsonThisTurn);
                metrics.RecordVerificationResult(verification.Passed);
                var verifiedMessages = messages.Append(
                    new LlmMessage(LlmRole.Assistant, [new LlmTextContent(verification.VerifiedAnswer)]));

                return new AgentTurnResult(
                    verification.VerifiedAnswer,
                    state with { Messages = [.. verifiedMessages] },
                    verification.ConstraintFlags,
                    verification.SuppressedClaims);
            }

            messages = [.. messages, new LlmMessage(LlmRole.Assistant, BuildAssistantContent(response))];

            LlmToolResultContent[] toolResults;
            try
            {
                toolResults = await Task.WhenAll(
                    response.ToolCalls.Select(call => DispatchAndLogAsync(state.Site, state.PatientId, call, operationToken)))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (deadlineOnlyCts.IsCancellationRequested)
            {
                return BuildDeterministicFallback(state, messages, toolResultJsonThisTurn, "Turn exceeded its configured deadline.");
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
