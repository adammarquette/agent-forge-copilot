using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Api.Chat;

/// <summary>
/// The browser-facing SignalR hub (ARCHITECTURE.md D11, ENGINEERING_STANDARDS.md §12). Every
/// method reads the caller's identity from the server-side session only - no bearer token ever
/// travels over this connection - and delegates the actual turn to
/// <see cref="ChatSessionCoordinator"/>, whose outbox is what makes <see cref="Resume"/> an
/// idempotent recovery path after a reconnect rather than a best-effort one.
/// </summary>
public sealed class ChatHub(ChatSessionCoordinator coordinator, ILogger<ChatHub> logger) : Hub
{
    private const int MaxDeliveryAttempts = 3;

    /// <summary>Starts the pre-visit brief for the authenticated session's patient (UC-1).</summary>
    public async Task RequestBrief()
    {
        var (sessionId, session) = await GetAuthenticatedSessionOrThrowAsync().ConfigureAwait(false);
        var message = await coordinator.RequestBriefAsync(sessionId, session, Context.ConnectionAborted).ConfigureAwait(false);
        await DeliverAsync(message).ConfigureAwait(false);
    }

    /// <summary>Asks a follow-up question within the authenticated session (UC-2).</summary>
    public async Task AskFollowUp(string question)
    {
        var (sessionId, session) = await GetAuthenticatedSessionOrThrowAsync().ConfigureAwait(false);
        var message = await coordinator.AskFollowUpAsync(sessionId, session, question, Context.ConnectionAborted).ConfigureAwait(false);
        await DeliverAsync(message).ConfigureAwait(false);
    }

    /// <summary>
    /// Called by a reconnecting client to replay anything sent while it was disconnected
    /// (ENGINEERING_STANDARDS.md §12 - idempotent resume: safe to call repeatedly with the same
    /// <paramref name="lastSeenSequence"/>, and safe across a new connection id after a drop).
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> Resume(long lastSeenSequence)
    {
        var (sessionId, _) = await GetAuthenticatedSessionOrThrowAsync().ConfigureAwait(false);
        return coordinator.Resume(sessionId, lastSeenSequence);
    }

    private async Task<(string SessionId, PatientSessionContext Session)> GetAuthenticatedSessionOrThrowAsync()
    {
        var httpContext = Context.GetHttpContext()
            ?? throw new HubException("No HTTP context available for this connection.");

        await httpContext.Session.LoadAsync(Context.ConnectionAborted).ConfigureAwait(false);
        var session = httpContext.Session.TryGetPatientSession()
            ?? throw new HubException("No authenticated session - complete the SMART launch first.");

        return (httpContext.Session.Id, session);
    }

    /// <summary>
    /// Bounded retry with backoff (ENGINEERING_STANDARDS.md §12); if every attempt fails, the
    /// message is not lost - it already sits in the outbox, so the next <see cref="Resume"/> call
    /// (this reconnect or a later one) recovers it. Logging here is the "reported to observers"
    /// half of "never silently discarded."
    /// </summary>
    private async Task DeliverAsync(ChatMessage message)
    {
        for (var attempt = 1; attempt <= MaxDeliveryAttempts; attempt++)
        {
            try
            {
                await Clients.Caller.SendAsync("ChatMessage", message, Context.ConnectionAborted).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < MaxDeliveryAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), Context.ConnectionAborted).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ChatHubLog.MessageDeliveryFailed(logger, message.Kind, message.Sequence, Context.ConnectionId, ex);
            }
        }
    }
}
