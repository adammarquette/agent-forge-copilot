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
    private const string SessionIdItemKey = "patient-session.id";
    private const string SessionItemKey = "patient-session.context";

    /// <summary>
    /// ASP.NET Core <c>Session</c> is a per-HTTP-request abstraction backed by a feature on
    /// <see cref="HttpContext"/>; a SignalR connection is not one request, so re-reading
    /// <c>Context.GetHttpContext().Session</c> from inside a hub method is unreliable - most
    /// visibly with the long-polling transport (forced by <c>BffQaFixture</c>, since the in-memory
    /// TestServer has no real sockets), where every poll is a distinct HTTP request and the one
    /// handling a given hub method invocation may never have run through the <c>UseSession()</c>
    /// middleware, throwing <see cref="InvalidOperationException"/> ("Session has not been
    /// configured for this application or request") instead of the intended "not authenticated"
    /// <see cref="HubException"/>. The <see cref="HubCallerContext"/> reflects the
    /// connection-establishing request, which did run the full middleware pipeline, so the
    /// session is loaded once here and cached on <see cref="HubCallerContext.Items"/> for every
    /// hub method on this connection to read instead.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext()
            ?? throw new HubException("No HTTP context available for this connection.");

        await httpContext.Session.LoadAsync(Context.ConnectionAborted).ConfigureAwait(false);
        Context.Items[SessionIdItemKey] = httpContext.Session.Id;
        if (httpContext.Session.TryGetPatientSession() is { } session)
        {
            Context.Items[SessionItemKey] = session;
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>Starts the pre-visit brief for the authenticated session's patient (UC-1).</summary>
    public async Task RequestBrief()
    {
        var (sessionId, session) = GetAuthenticatedSessionOrThrow();
        var message = await coordinator.RequestBriefAsync(
            sessionId, session, Context.ConnectionAborted, CreateStatusProgress()).ConfigureAwait(false);
        await DeliverAsync(message).ConfigureAwait(false);
    }

    /// <summary>Asks a follow-up question within the authenticated session (UC-2).</summary>
    public async Task AskFollowUp(string question)
    {
        var (sessionId, session) = GetAuthenticatedSessionOrThrow();
        var message = await coordinator.AskFollowUpAsync(
            sessionId, session, question, Context.ConnectionAborted, CreateStatusProgress()).ConfigureAwait(false);
        await DeliverAsync(message).ConfigureAwait(false);
    }

    // Streams the orchestrator's tool-call status to the caller as interim "ChatStatus" messages while the
    // (verified) answer is assembled - perceived-latency only, never unverified content. Fire-and-forget: a
    // dropped status is harmless, since the answer + Resume outbox remain the durable delivery path.
    // reference: gitlab#126.
    private Progress<string> CreateStatusProgress()
    {
        var caller = Clients.Caller;
        var connectionAborted = Context.ConnectionAborted;
        return new Progress<string>(status => _ = caller.SendAsync("ChatStatus", status, connectionAborted));
    }

    /// <summary>
    /// Called by a reconnecting client to replay anything sent while it was disconnected
    /// (ENGINEERING_STANDARDS.md §12 - idempotent resume: safe to call repeatedly with the same
    /// <paramref name="lastSeenSequence"/>, and safe across a new connection id after a drop).
    /// </summary>
    public Task<IReadOnlyList<ChatMessage>> Resume(long lastSeenSequence)
    {
        var (sessionId, _) = GetAuthenticatedSessionOrThrow();
        return Task.FromResult(coordinator.Resume(sessionId, lastSeenSequence));
    }

    private (string SessionId, PatientSessionContext Session) GetAuthenticatedSessionOrThrow()
    {
        if (Context.Items.TryGetValue(SessionItemKey, out var value) && value is PatientSessionContext session)
        {
            return ((string)Context.Items[SessionIdItemKey]!, session);
        }

        throw new HubException("No authenticated session - complete the SMART launch first.");
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
