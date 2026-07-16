using System.Text.Json;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>
/// The hub-independent core of a chat turn: sets the session's token in scope, runs the
/// orchestrator, persists the resulting conversation state, and appends the answer to the outbox.
/// Kept independent of <see cref="Microsoft.AspNetCore.SignalR.Hub"/> so it is directly
/// unit-testable; <see cref="ChatHub"/> is the thin layer that reads the session and pushes the
/// result to the caller.
/// </summary>
public sealed class ChatSessionCoordinator(
    IAgentOrchestrator orchestrator,
    IConversationStateStore conversationStore,
    IChatMessageOutbox outbox,
    IScopedAccessTokenProvider tokenProvider,
    IScopedClinicianIdentityAccessor clinicianIdentityAccessor,
    ICorrelationIdAccessor correlationIdAccessor,
    IDerivedFactStore factStore,
    ILogger<ChatSessionCoordinator> logger)
{
    /// <summary>Starts the pre-visit brief for <paramref name="session"/> and returns the message appended to the outbox.</summary>
    public async Task<ChatMessage> RequestBriefAsync(string sessionId, PatientSessionContext session, CancellationToken cancellationToken)
    {
        tokenProvider.AccessToken = session.AccessToken;
        clinicianIdentityAccessor.ClinicianIdentity = session.ClinicianIdentity;

        using var scope = BeginCorrelationScope();
        var result = await orchestrator.StartBriefAsync(session.Site, session.PatientId, cancellationToken).ConfigureAwait(false);

        conversationStore.Save(sessionId, result.State);
        var payload = ToPayload(result, await LoadDocumentCitationsAsync(session.PatientId, cancellationToken).ConfigureAwait(false));
        return outbox.Append(sessionId, "brief", JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// Asks a follow-up for <paramref name="session"/>, resuming the session's saved conversation
    /// state if one exists, or starting fresh (scoped to the same patient) if not.
    /// </summary>
    public async Task<ChatMessage> AskFollowUpAsync(
        string sessionId, PatientSessionContext session, string question, CancellationToken cancellationToken)
    {
        tokenProvider.AccessToken = session.AccessToken;
        clinicianIdentityAccessor.ClinicianIdentity = session.ClinicianIdentity;

        using var scope = BeginCorrelationScope();
        var state = conversationStore.TryGet(sessionId) ?? ConversationState.Start(session.Site, session.PatientId);
        var result = await orchestrator.AskFollowUpAsync(state, question, cancellationToken).ConfigureAwait(false);

        conversationStore.Save(sessionId, result.State);
        var payload = ToPayload(result, await LoadDocumentCitationsAsync(session.PatientId, cancellationToken).ConfigureAwait(false));
        return outbox.Append(sessionId, "answer", JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// Opens the logging scope every downstream call in this turn (tool dispatch, the LLM call,
    /// verification) inherits, so the correlation id reaches every log line without being threaded
    /// as an explicit parameter through each of those layers (FR-OBS-1, ENGINEERING_STANDARDS.md
    /// Sec.7).
    /// </summary>
    private IDisposable? BeginCorrelationScope() =>
        logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationIdAccessor.CorrelationId });

    /// <summary>Returns every message for <paramref name="sessionId"/> after <paramref name="lastSeenSequence"/>.</summary>
    public IReadOnlyList<ChatMessage> Resume(string sessionId, long lastSeenSequence) =>
        outbox.GetSince(sessionId, lastSeenSequence);

    // The pre-visit brief must surface facts ingested before the visit (UC-6), and the client needs each
    // fact's source-document id + region to open the PDF and highlight it (FR-CITE-2). Read-only; empty when none.
    private async Task<IReadOnlyList<DocumentCitation>> LoadDocumentCitationsAsync(string patientId, CancellationToken cancellationToken) =>
        DerivedFactCitationProjector.Project(
            await factStore.GetByPatientAsync(patientId, cancellationToken).ConfigureAwait(false));

    private static ChatAnswerPayload ToPayload(AgentTurnResult result, IReadOnlyList<DocumentCitation> documentCitations) => new(
        result.Answer,
        [.. result.SafetyFlags.Select(f => new SafetyFlagPayload(f.RuleId, f.Description, [.. f.Sources.Select(s => s.Citation)]))],
        [.. result.SuppressedClaims.Select(c => new SuppressedClaimPayload(c.Line, c.Reason))],
        documentCitations,
        result.IsDeterministicFallback);
}
