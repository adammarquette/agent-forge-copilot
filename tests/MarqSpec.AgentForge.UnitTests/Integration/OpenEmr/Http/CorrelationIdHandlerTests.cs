using System.Net;
using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Http;
using MarqSpec.AgentForge.UnitTests.TestSupport;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Http;

public sealed class CorrelationIdHandlerTests
{
    [Fact]
    public async Task SendAsync_Always_AddsCorrelationIdHeaderFromAccessor()
    {
        var accessor = A.Fake<ICorrelationIdAccessor>();
        A.CallTo(() => accessor.CorrelationId).Returns("corr-abc-123");
        var capturing = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new CorrelationIdHandler(accessor) { InnerHandler = capturing };
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://emr.example.org/apis/default/fhir/Patient/1"),
            CancellationToken.None);

        capturing.LastRequest!.Headers.GetValues(CorrelationIdHandler.HeaderName)
            .Should().ContainSingle().Which.Should().Be("corr-abc-123");
    }

    [Fact]
    public async Task SendAsync_RequestAlreadyCarriesCorrelationIdHeader_ReplacesItWithAccessorValue()
    {
        var accessor = A.Fake<ICorrelationIdAccessor>();
        A.CallTo(() => accessor.CorrelationId).Returns("authoritative-id");
        var capturing = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new CorrelationIdHandler(accessor) { InnerHandler = capturing };
        using var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://emr.example.org/apis/default/fhir/Patient/1");
        request.Headers.Add(CorrelationIdHandler.HeaderName, "stale-id");

        await invoker.SendAsync(request, CancellationToken.None);

        capturing.LastRequest!.Headers.GetValues(CorrelationIdHandler.HeaderName)
            .Should().ContainSingle().Which.Should().Be("authoritative-id");
    }
}
