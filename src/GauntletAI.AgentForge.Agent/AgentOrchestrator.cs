using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// The multi-turn agent loop (ARCHITECTURE.md §8, D9). Turn 1 plans a bounded set of parallel
/// tool calls for the initial brief; follow-up turns maintain history so the model can resolve
/// references ("her", "that lab") and chain tools when one result is needed before the next call.
/// Grounding discipline and refusal boundaries live in <see cref="CardiologyProfile"/> (the
/// prompt); patient-scoping enforcement lives in <see cref="McpToolCatalog"/> and the tool
/// dispatcher (below the model). Every draft answer passes through <see cref="IClinicalResponseVerifier"/>
/// before it becomes an <see cref="AgentTurnResult"/> - the mandatory gate FR-VERIF-0 requires;
/// nothing returned by this class bypasses it.
/// </summary>
public sealed class AgentOrchestrator(
    ILlmProvider llmProvider,
    IMcpToolDispatcher toolDispatcher,
    IClinicalResponseVerifier verifier,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    /// <summary>
    /// Safety bound on tool-call rounds within a single turn. NFR-REL-1's full graceful-degradation
    /// story (return the verified core instead of failing outright) is Epic 10's scope; for now,
    /// exceeding this throws rather than looping indefinitely against a misbehaving model.
    /// </summary>
    private const int MaxToolCallRounds = 5;

    private const string BriefRequestPrompt =
        "Give me the pre-visit brief for this patient: what changed since the last visit and what matters today.";

    /// <summary>Starts a new session for a patient and generates the initial pre-visit brief (UC-1).</summary>
    public Task<AgentTurnResult> StartBriefAsync(string site, string patientId, CancellationToken cancellationToken)
    {
        var state = ConversationState.Start(site, patientId) with
        {
            Messages = [LlmMessage.FromText(LlmRole.User, BriefRequestPrompt)],
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
        var messages = state.Messages;
        List<string> toolResultJsonThisTurn = [];

        for (var round = 0; round < MaxToolCallRounds; round++)
        {
            var request = new LlmRequest(CardiologyProfile.SystemPrompt, messages, McpToolCatalog.AllTools);
            var response = await llmProvider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StopReason != LlmStopReason.ToolUse || response.ToolCalls.Count == 0)
            {
                var verification = verifier.Verify(response.Content, toolResultJsonThisTurn);
                var verifiedMessages = messages.Append(
                    new LlmMessage(LlmRole.Assistant, [new LlmTextContent(verification.VerifiedAnswer)]));

                return new AgentTurnResult(
                    verification.VerifiedAnswer, state with { Messages = [.. verifiedMessages] }, verification.ConstraintFlags);
            }

            messages = [.. messages, new LlmMessage(LlmRole.Assistant, BuildAssistantContent(response))];

            var toolResults = await Task.WhenAll(
                response.ToolCalls.Select(call => DispatchAndLogAsync(state.Site, state.PatientId, call, cancellationToken)))
                .ConfigureAwait(false);

            toolResultJsonThisTurn.AddRange(toolResults.Select(r => r.ResultJson));
            messages = [.. messages, new LlmMessage(LlmRole.User, toolResults)];
        }

        throw new InvalidOperationException(
            $"Exceeded {MaxToolCallRounds} tool-call rounds without a final answer.");
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
