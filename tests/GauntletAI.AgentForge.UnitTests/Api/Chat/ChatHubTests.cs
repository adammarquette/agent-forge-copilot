using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Chat;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.UnitTests.Api.Chat;

public sealed class ChatHubTests : IDisposable
{
    private readonly IAgentOrchestrator _orchestrator = A.Fake<IAgentOrchestrator>();
    private readonly IConversationStateStore _conversationStore = A.Fake<IConversationStateStore>();
    private readonly IChatMessageOutbox _outbox = A.Fake<IChatMessageOutbox>();
    private readonly ChatHub _sut;

    public ChatHubTests()
    {
        var coordinator = new ChatSessionCoordinator(
            _orchestrator,
            _conversationStore,
            _outbox,
            A.Fake<IScopedAccessTokenProvider>(),
            A.Fake<IScopedClinicianIdentityAccessor>(),
            A.Fake<ICorrelationIdAccessor>(),
            A.Fake<GauntletAI.AgentForge.Data.IDerivedFactStore>(),
            A.Fake<ILogger<ChatSessionCoordinator>>());

        _sut = new ChatHub(coordinator, A.Fake<ILogger<ChatHub>>());
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Resume_CalledTwiceAfterConnect_BothCallsUseTheSessionCapturedAtConnectTimeRatherThanRereadingHttpContext()
    {
        // Regression test: SignalR hub method invocations don't reliably see a fresh
        // HttpContext.Session - most visibly over long-polling (forced by BffQaFixture, since the
        // in-memory TestServer has no real sockets), where every poll is a distinct HTTP request.
        // Re-reading Context.GetHttpContext().Session from inside a hub method throws
        // InvalidOperationException ("Session has not been configured for this application or
        // request") on any request that never ran the UseSession() middleware, instead of the
        // intended "not authenticated" HubException.
        //
        // Simulates that reality: Context.Features returns the connection-establishing request's
        // features (a real session) on its first access, then a bare request with no session
        // feature at all on every access after that - the shape of a second, unrelated long-poll.
        // The un-fixed hub reads Context.GetHttpContext().Session fresh on every method call, so
        // its first Resume() call (the 1st-ever Features access) still succeeds by accident and
        // only the *second* call trips over the missing feature - proving the fix requires a
        // second call to actually distinguish it from the old behavior, not just one.
        var session = new PatientSessionContext("token-abc", "default", "123", "dr-jones");
        var connectingRequestFeatures = BuildHttpContextWithSession(session, "session-xyz").Features;
        var laterPollRequestFeatures = BuildHttpContext().Features; // no ISessionFeature set

        var hubContext = A.Fake<HubCallerContext>();
        A.CallTo(() => hubContext.Items).Returns(new Dictionary<object, object?>());
        A.CallTo(() => hubContext.ConnectionAborted).Returns(CancellationToken.None);
        A.CallTo(() => hubContext.Features).ReturnsNextFromSequence(connectingRequestFeatures, laterPollRequestFeatures);
        _sut.Context = hubContext;

        await _sut.OnConnectedAsync();
        var first = () => _sut.Resume(5);
        var second = () => _sut.Resume(6);

        await first.Should().NotThrowAsync();
        await second.Should().NotThrowAsync();
        A.CallTo(() => _outbox.GetSince("session-xyz", 5)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _outbox.GetSince("session-xyz", 6)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Resume_ConnectedWithNoAuthenticatedSession_ThrowsHubException()
    {
        // The ASP.NET Core session middleware attaches an (empty) session to every request
        // regardless of whether a patient session was ever saved into it - this is the real
        // "browser hasn't completed the SMART launch yet" shape, distinct from the missing-feature
        // shape the other test simulates.
        var hubContext = A.Fake<HubCallerContext>();
        A.CallTo(() => hubContext.Items).Returns(new Dictionary<object, object?>());
        A.CallTo(() => hubContext.ConnectionAborted).Returns(CancellationToken.None);
        A.CallTo(() => hubContext.Features).Returns(BuildHttpContextWithSession(session: null, "session-xyz").Features);
        _sut.Context = hubContext;

        await _sut.OnConnectedAsync();
        var act = () => _sut.Resume(5);

        await act.Should().ThrowAsync<HubException>().WithMessage("No authenticated session*");
    }

    private static DefaultHttpContext BuildHttpContextWithSession(PatientSessionContext? session, string sessionId)
    {
        var httpContext = BuildHttpContext();
        var inMemorySession = new InMemoryTestSession(sessionId);
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature(inMemorySession));
        if (session is not null)
        {
            inMemorySession.SavePatientSession(session);
        }

        return httpContext;
    }

    /// <summary>
    /// A bare <see cref="DefaultHttpContext"/> does not register itself as
    /// <see cref="IHttpContextFeature"/> on its own <see cref="HttpContext.Features"/> - the
    /// mechanism <c>HubCallerContext.GetHttpContext()</c> relies on - so tests must wire it up
    /// explicitly to get a context back at all.
    /// </summary>
    private static DefaultHttpContext BuildHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpContextFeature>(new TestHttpContextFeature(httpContext));
        return httpContext;
    }

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestHttpContextFeature(HttpContext httpContext) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }

    /// <summary>Minimal in-process <see cref="ISession"/> - no distributed cache plumbing needed for these tests.</summary>
    private sealed class InMemoryTestSession(string id) : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public bool IsAvailable => true;
        public string Id { get; } = id;
        public IEnumerable<string> Keys => _store.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
        public void Set(string key, byte[] value) => _store[key] = value;
        public void Remove(string key) => _store.Remove(key);
        public void Clear() => _store.Clear();
    }
}
