using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Api.Mcp;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Api.Mcp;

public sealed class McpToolServerGetDocumentsTests
{
    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly ICorrelationIdAccessor _correlationIdAccessor = A.Fake<ICorrelationIdAccessor>();
    private readonly McpToolServer _sut;

    public McpToolServerGetDocumentsTests()
    {
        A.CallTo(() => _correlationIdAccessor.CorrelationId).Returns("corr-1");
        _sut = new McpToolServer(_fhirClient, _correlationIdAccessor, NullLogger<McpToolServer>.Instance);
    }

    [Fact]
    public async Task GetDocumentsAsync_ValidRequest_CombinesDiagnosticReportsAndDocumentReferences()
    {
        var reports = new List<ClinicalDocumentRecord>
        {
            new(new ClinicalSourceRef("DiagnosticReport", "1"), "Echocardiogram", "final", null, "LVEF 35-40%"),
        };
        var docs = new List<ClinicalDocumentRecord>
        {
            new(new ClinicalSourceRef("DocumentReference", "2"), "Device Interrogation Report", "current", null, "Battery 4.2V"),
        };
        A.CallTo(() => _fhirClient.GetDiagnosticReportsAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ClinicalDocumentRecord>>(reports));
        A.CallTo(() => _fhirClient.GetDocumentReferencesAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ClinicalDocumentRecord>>(docs));

        var result = await _sut.GetDocumentsAsync(
            new GetDocumentsRequest { Site = "default", PatientId = "1" }, CancellationToken.None);

        result.Documents.Should().HaveCount(2);
        result.Documents.Should().Contain(reports[0]);
        result.Documents.Should().Contain(docs[0]);
    }

    [Fact]
    public async Task GetDocumentsAsync_DocumentTypeFilterProvided_ReturnsOnlyMatchingDocumentType()
    {
        var reports = new List<ClinicalDocumentRecord>
        {
            new(new ClinicalSourceRef("DiagnosticReport", "1"), "Echocardiogram", "final", null, "LVEF 35-40%"),
        };
        var docs = new List<ClinicalDocumentRecord>
        {
            new(new ClinicalSourceRef("DocumentReference", "2"), "Device Interrogation Report", "current", null, "Battery 4.2V"),
        };
        A.CallTo(() => _fhirClient.GetDiagnosticReportsAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ClinicalDocumentRecord>>(reports));
        A.CallTo(() => _fhirClient.GetDocumentReferencesAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ClinicalDocumentRecord>>(docs));

        var result = await _sut.GetDocumentsAsync(
            new GetDocumentsRequest { Site = "default", PatientId = "1", DocumentType = "echo" }, CancellationToken.None);

        result.Documents.Should().ContainSingle().Which.DocumentType.Should().Be("Echocardiogram");
    }

    [Fact]
    public async Task GetDocumentsAsync_InvalidRequest_ThrowsContractExceptionWithoutCallingFhirClient()
    {
        var act = () => _sut.GetDocumentsAsync(
            new GetDocumentsRequest { Site = "default", PatientId = string.Empty }, CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }
}
