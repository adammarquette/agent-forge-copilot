using System.Net;
using System.Net.Http.Headers;
using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

/// <summary>
/// Unit tests for <see cref="OpenEmrFhirClient"/>'s FHIR <c>Binary</c> download (gitlab#109) — the source
/// bytes behind a <c>DocumentReference</c>, needed to render the production click-to-source overlay. Guarded
/// behavior: a success response yields the bytes + media type; a not-found (or any non-success) yields null so
/// the caller degrades rather than serving a broken document.
/// </summary>
public sealed class OpenEmrFhirClientBinaryTests
{
    private readonly IOpenEmrFhirApi _api = A.Fake<IOpenEmrFhirApi>();

    private OpenEmrFhirClient Sut() => new(_api);

    [Fact]
    public async Task GetBinaryAsync_WhenFound_ReturnsBytesAndMediaType()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(pdf) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        A.CallTo(() => _api.GetBinaryAsync("default", "doc-7", A<CancellationToken>._)).Returns(response);

        var result = await Sut().GetBinaryAsync("default", "doc-7", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Content.Should().Equal(pdf);
        result.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task GetBinaryAsync_WhenNotFound_ReturnsNull()
    {
        A.CallTo(() => _api.GetBinaryAsync("default", "missing", A<CancellationToken>._))
            .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await Sut().GetBinaryAsync("default", "missing", CancellationToken.None);

        result.Should().BeNull();
    }
}
