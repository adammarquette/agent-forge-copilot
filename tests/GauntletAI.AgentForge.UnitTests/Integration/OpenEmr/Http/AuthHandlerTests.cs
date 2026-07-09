using System.Net;
using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using GauntletAI.AgentForge.UnitTests.TestSupport;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Http;

public sealed class AuthHandlerTests
{
    [Fact]
    public async Task SendAsync_TokenProviderReturnsToken_AttachesBearerAuthorizationHeader()
    {
        var tokenProvider = A.Fake<IAccessTokenProvider>();
        A.CallTo(() => tokenProvider.GetAccessTokenAsync(A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<string?>("test-token-123"));
        var capturing = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new AuthHandler(tokenProvider) { InnerHandler = capturing };
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://emr.example.org/apis/default/fhir/Patient/1"),
            CancellationToken.None);

        capturing.LastRequest!.Headers.Authorization.Should().NotBeNull();
        capturing.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturing.LastRequest.Headers.Authorization.Parameter.Should().Be("test-token-123");
    }

    [Fact]
    public async Task SendAsync_TokenProviderReturnsNull_ThrowsRatherThanSendingAnUnauthenticatedRequest()
    {
        // FR-AUTH-1: "an unauthenticated request is rejected before any tool runs" - rejected by
        // *this* layer, deterministically, not left to OpenEMR's own 401 to catch after the fact.
        var tokenProvider = A.Fake<IAccessTokenProvider>();
        A.CallTo(() => tokenProvider.GetAccessTokenAsync(A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<string?>(null));
        var capturing = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new AuthHandler(tokenProvider) { InnerHandler = capturing };
        using var invoker = new HttpMessageInvoker(handler);

        var act = () => invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://emr.example.org/apis/default/fhir/Patient/1"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthenticatedRequestException>();
        capturing.LastRequest.Should().BeNull("the outbound call must never be attempted without a token");
    }

    [Fact]
    public async Task SendAsync_Always_PassesCancellationTokenToTokenProvider()
    {
        var tokenProvider = A.Fake<IAccessTokenProvider>();
        A.CallTo(() => tokenProvider.GetAccessTokenAsync(A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<string?>("t"));
        var capturing = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new AuthHandler(tokenProvider) { InnerHandler = capturing };
        using var invoker = new HttpMessageInvoker(handler);
        using var cts = new CancellationTokenSource();

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://emr.example.org/apis/default/fhir/Patient/1"),
            cts.Token);

        A.CallTo(() => tokenProvider.GetAccessTokenAsync(cts.Token)).MustHaveHappenedOnceExactly();
    }
}
