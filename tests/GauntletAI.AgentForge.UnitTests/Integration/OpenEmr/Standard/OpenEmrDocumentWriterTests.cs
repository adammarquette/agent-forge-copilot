using System.Net;
using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using GauntletAI.AgentForge.Integration.OpenEmr.Standard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Standard;

/// <summary>
/// Drives <see cref="OpenEmrDocumentWriter"/>: the standard-API document write must succeed on 2xx, degrade
/// (not throw) on OpenEMR failures, and distinguish an auth/scope gap from a transient failure so the caller
/// can act. reference: documentation/W2_ARCHITECTURE.md §4, agent-forge#42/#43
/// </summary>
public sealed class OpenEmrDocumentWriterTests
{
    private static OpenEmrDocumentWriter CreateWriter(IOpenEmrDocumentApi api) =>
        new(
            api,
            Options.Create(new OpenEmrOptions
            {
                BaseUrl = "https://emr.example/",
                Site = "default",
                ClientId = "cid",
                Scopes = [],
            }),
            A.Fake<ILogger<OpenEmrDocumentWriter>>());

    private static DocumentWriteRequest SampleRequest() => new()
    {
        PatientId = "p-1",
        FileName = "lab.pdf",
        Content = [1, 2, 3],
        MediaType = "application/pdf",
        CategoryPath = "AgentForge",
    };

    private static void ArrangeResponse(IOpenEmrDocumentApi api, HttpStatusCode status) =>
        A.CallTo(() => api.UploadDocumentAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<ByteArrayPart>._, A<CancellationToken>._))
            .Returns(new HttpResponseMessage(status));

    [Fact]
    public async Task WriteAsync_WhenApiReturnsSuccess_ReturnsWritten()
    {
        var api = A.Fake<IOpenEmrDocumentApi>();
        ArrangeResponse(api, HttpStatusCode.OK);

        var result = await CreateWriter(api).WriteAsync(SampleRequest());

        result.Status.Should().Be(DocumentWriteStatus.Written);
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task WriteAsync_WhenApiReturnsAuthFailure_ReturnsUnauthorized(HttpStatusCode status)
    {
        var api = A.Fake<IOpenEmrDocumentApi>();
        ArrangeResponse(api, status);

        var result = await CreateWriter(api).WriteAsync(SampleRequest());

        result.Status.Should().Be(DocumentWriteStatus.Unauthorized);
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_WhenApiReturnsServerError_ReturnsFailed()
    {
        var api = A.Fake<IOpenEmrDocumentApi>();
        ArrangeResponse(api, HttpStatusCode.InternalServerError);

        var result = await CreateWriter(api).WriteAsync(SampleRequest());

        result.Status.Should().Be(DocumentWriteStatus.Failed);
    }

    [Fact]
    public async Task WriteAsync_WhenNoAdminToken_ReturnsUnauthorized()
    {
        var api = A.Fake<IOpenEmrDocumentApi>();
        A.CallTo(() => api.UploadDocumentAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<ByteArrayPart>._, A<CancellationToken>._))
            .Throws(new UnauthenticatedRequestException("no admin token"));

        var result = await CreateWriter(api).WriteAsync(SampleRequest());

        result.Status.Should().Be(DocumentWriteStatus.Unauthorized);
    }

    [Fact]
    public async Task WriteAsync_WhenTransportFails_ReturnsFailed()
    {
        var api = A.Fake<IOpenEmrDocumentApi>();
        A.CallTo(() => api.UploadDocumentAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<ByteArrayPart>._, A<CancellationToken>._))
            .Throws(new HttpRequestException("connection reset"));

        var result = await CreateWriter(api).WriteAsync(SampleRequest());

        result.Status.Should().Be(DocumentWriteStatus.Failed);
    }

    [Fact]
    public async Task WriteAsync_WhenCancelled_Propagates()
    {
        var api = A.Fake<IOpenEmrDocumentApi>();
        A.CallTo(() => api.UploadDocumentAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<ByteArrayPart>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        var act = () => CreateWriter(api).WriteAsync(SampleRequest());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteAsync_WhenWriting_PassesSitePatientAndCategory()
    {
        var api = A.Fake<IOpenEmrDocumentApi>();
        ArrangeResponse(api, HttpStatusCode.OK);

        await CreateWriter(api).WriteAsync(SampleRequest());

        A.CallTo(() => api.UploadDocumentAsync(
                "default", "p-1", "AgentForge", A<string?>._, A<ByteArrayPart>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
