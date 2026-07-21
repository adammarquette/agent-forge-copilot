using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Agents.Ingestion;
using MarqSpec.AgentForge.Data;
using MarqSpec.AgentForge.Data.Entities;
using MarqSpec.AgentForge.Documents;
using MarqSpec.AgentForge.Llm;
using MarqSpec.AgentForge.Observability;
using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.UnitTests.Agents.Ingestion;

/// <summary>
/// Drives <see cref="DocumentIngestionService"/> — the pre-visit ingestion. The document is already in
/// OpenEMR (the front desk uploaded it natively), so the sidecar only extracts + persists facts citing the
/// supplied DocumentReference id. Verifies content-hash idempotency (skip re-work), the extraction-rejected
/// degrade path (persist nothing), and the happy path (persist facts with the source-document lineage).
/// reference: documentation/W2_ARCHITECTURE.md §4
/// </summary>
public sealed class DocumentIngestionServiceTests
{
    private readonly IDocumentExtractor _extractor = A.Fake<IDocumentExtractor>();
    private readonly IDerivedFactStore _store = A.Fake<IDerivedFactStore>();
    private readonly IDerivedFactMapper _mapper = A.Fake<IDerivedFactMapper>();
    private readonly IAgentForgeMetrics _metrics = A.Fake<IAgentForgeMetrics>();

    public DocumentIngestionServiceTests()
    {
        A.CallTo(() => _store.FindByContentHashAsync(A<string>._, A<CancellationToken>._))
            .Returns((IngestedDocument?)null);
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Ok(ClinicalDocumentType.LabPdf, "{\"tests\":[]}", new LlmUsage(0, 0, 0m)));
        A.CallTo(() => _mapper.Map(A<DocumentExtractionResult>._, A<string?>._))
            .Returns(new List<DerivedFact> { Fact(), Fact() });
    }

    private DocumentIngestionService Service() =>
        new(_extractor, _store, _mapper, _metrics, TimeProvider.System, A.Fake<ILogger<DocumentIngestionService>>());

    private static DerivedFact Fact() => new()
    {
        FactType = "lab.result",
        PayloadJson = "{}",
        Citation = new Citation { SourceType = CitationSourceType.Derived, SourceId = "dr-1" },
    };

    private static DocumentIngestionRequest Request() => new()
    {
        PatientId = "p-1",
        DocumentReferenceId = "dr-1",
        DocumentType = ClinicalDocumentType.LabPdf,
        Content = [1, 2, 3],
        MediaType = "application/pdf",
    };

    [Fact]
    public async Task IngestAsync_WhenContentAlreadyIngested_ReturnsAlreadyIngested_AndSkipsExtraction()
    {
        var existing = new IngestedDocument
        {
            PatientId = "p-1",
            ContentHash = ContentHash.Compute([1, 2, 3]),
            OpenEmrDocumentReferenceId = "dr-1",
            DerivedFacts = [Fact()],
        };
        A.CallTo(() => _store.FindByContentHashAsync(A<string>._, A<CancellationToken>._)).Returns(existing);

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.AlreadyIngested);
        result.FactCount.Should().Be(1);
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _store.AddAsync(A<IngestedDocument>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _metrics.RecordDocumentIngestion("already_ingested", A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestAsync_WhenExtractionRejected_ReturnsRejected_AndDoesNotPersist()
    {
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Rejected(ClinicalDocumentType.LabPdf, "schema violation"));

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.ExtractionRejected);
        A.CallTo(() => _store.AddAsync(A<IngestedDocument>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _metrics.RecordDocumentIngestion("extraction_rejected", A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestAsync_OnSuccess_PersistsFactsCitingTheSuppliedDocumentReference()
    {
        IngestedDocument? persisted = null;
        A.CallTo(() => _store.AddAsync(A<IngestedDocument>._, A<CancellationToken>._))
            .Invokes((IngestedDocument d, CancellationToken _) => persisted = d);

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.Ingested);
        result.DocumentReferenceId.Should().Be("dr-1");
        result.FactCount.Should().Be(2);

        persisted.Should().NotBeNull();
        persisted!.ContentHash.Should().Be(ContentHash.Compute([1, 2, 3]));
        persisted.OpenEmrDocumentReferenceId.Should().Be("dr-1");
        persisted.DerivedFacts.Should().HaveCount(2);
        A.CallTo(() => _mapper.Map(A<DocumentExtractionResult>._, "dr-1")).MustHaveHappenedOnceExactly();
        A.CallTo(() => _metrics.RecordDocumentIngestion("ingested", A<TimeSpan>._)).MustHaveHappenedOnceExactly();
    }
}
