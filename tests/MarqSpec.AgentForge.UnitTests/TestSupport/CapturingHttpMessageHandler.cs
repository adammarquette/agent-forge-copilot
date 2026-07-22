namespace MarqSpec.AgentForge.UnitTests.TestSupport;

/// <summary>
/// Terminal <see cref="HttpMessageHandler"/> test double that records the request it received
/// (after passing through any <see cref="DelegatingHandler"/>s under test) and returns a
/// canned response.
/// </summary>
internal sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(respond(request));
    }
}
