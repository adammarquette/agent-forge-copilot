using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agents.Ingestion;
using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Standard;
using GauntletAI.AgentForge.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.UnitTests.Agents.Ingestion;

/// <summary>
/// Drives <see cref="DocumentIngestionService"/> — the write-back orchestration. Verifies content-hash
/// idempotency (skip re-work), the two degrade paths (extraction rejected / write failed persist nothing),
/// the happy path (persist with the resolved DocumentReference id + facts), the citation-pending fallback,
/// and that the snapshot is taken before the write. reference: documentation/W2_ARCHITECTURE.md §4
/// </summary>
public sealed class DocumentIngestionServiceTests
{
    private readonly IDocumentExtractor _extractor = A.Fake<IDocumentExtractor>();
    private readonly IOpenEmrDocumentWriter _writer = A.Fake<IOpenEmrDocumentWriter>();
    private readonly IDocumentReferenceResolver _resolver = A.Fake<IDocumentReferenceResolver>();
    private readonly IDerivedFactStore _store = A.Fake<IDerivedFactStore>();
    private readonly IDerivedFactMapper _mapper = A.Fake<IDerivedFactMapper>();

    public DocumentIngestionServiceTests()
    {
        // Happy-path defaults; individual tests override what they exercise.
        A.CallTo(() => _store.FindByContentHashAsync(A<string>._, A<CancellationToken>._))
            .Returns((IngestedDocument?)null);
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Ok(ClinicalDocumentType.LabPdf, "{\"tests\":[]}", new LlmUsage(0, 0, 0m)));
        A.CallTo(() => _resolver.SnapshotAsync(A<string>._, A<CancellationToken>._))
            .Returns(new HashSet<string>());
        A.CallTo(() => _writer.WriteAsync(A<DocumentWriteRequest>._, A<CancellationToken>._))
            .Returns(DocumentWriteResult.Written());
        A.CallTo(() => _resolver.ResolveNewAsync(A<string>._, A<IReadOnlySet<string>>._, A<CancellationToken>._))
            .Returns(new ClinicalSourceRef("DocumentReference", "dr-1"));
        A.CallTo(() => _mapper.Map(A<DocumentExtractionResult>._, A<string?>._))
            .Returns(new List<DerivedFact> { Fact(), Fact() });
    }

    private DocumentIngestionService Service() =>
        new(
            _extractor, _writer, _resolver, _store, _mapper,
            Options.Create(new DocumentIngestionOptions()),
            TimeProvider.System,
            A.Fake<ILogger<DocumentIngestionService>>());

    private static DerivedFact Fact() => new()
    {
        FactType = "lab.result",
        PayloadJson = "{}",
        Citation = new Citation { SourceType = CitationSourceType.Derived, SourceId = "x" },
    };

    private static DocumentIngestionRequest Request() => new()
    {
        PatientId = "p-1",
        DocumentType = ClinicalDocumentType.LabPdf,
        Content = [1, 2, 3],
        MediaType = "application/pdf",
        FileName = "lab.pdf",
    };

    [Fact]
    public async Task IngestAsync_WhenContentAlreadyIngested_ReturnsAlreadyIngested_AndSkipsExtractionAndWrite()
    {
        var existing = new IngestedDocument
        {
            PatientId = "p-1",
            ContentHash = ContentHash.Compute([1, 2, 3]),
            OpenEmrDocumentReferenceId = "dr-existing",
            DerivedFacts = [Fact()],
        };
        A.CallTo(() => _store.FindByContentHashAsync(A<string>._, A<CancellationToken>._)).Returns(existing);

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.AlreadyIngested);
        result.DocumentReferenceId.Should().Be("dr-existing");
        result.FactCount.Should().Be(1);
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _writer.WriteAsync(A<DocumentWriteRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestAsync_WhenExtractionRejected_ReturnsRejected_AndDoesNotWriteOrPersist()
    {
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Rejected(ClinicalDocumentType.LabPdf, "schema violation"));

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.ExtractionRejected);
        A.CallTo(() => _writer.WriteAsync(A<DocumentWriteRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _store.AddAsync(A<IngestedDocument>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestAsync_WhenWriteFails_ReturnsWriteFailed_AndDoesNotPersist()
    {
        A.CallTo(() => _writer.WriteAsync(A<DocumentWriteRequest>._, A<CancellationToken>._))
            .Returns(DocumentWriteResult.Failed("OpenEMR 500"));

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.WriteFailed);
        A.CallTo(() => _store.AddAsync(A<IngestedDocument>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestAsync_WhenSuccessAndCitationResolved_PersistsDocumentWithReferenceIdAndFacts()
    {
        IngestedDocument? persisted = null;
        A.CallTo(() => _store.AddAsync(A<IngestedDocument>._, A<CancellationToken>._))
            .Invokes((IngestedDocument d, CancellationToken _) => persisted = d);

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.Ingested);
        result.DocumentReferenceId.Should().Be("dr-1");
        result.CitationPending.Should().BeFalse();
        result.FactCount.Should().Be(2);

        persisted.Should().NotBeNull();
        persisted!.ContentHash.Should().Be(ContentHash.Compute([1, 2, 3]));
        persisted.OpenEmrDocumentReferenceId.Should().Be("dr-1");
        persisted.DerivedFacts.Should().HaveCount(2);
        A.CallTo(() => _mapper.Map(A<DocumentExtractionResult>._, "dr-1")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestAsync_WhenCitationUnresolved_PersistsWithPendingCitation()
    {
        A.CallTo(() => _resolver.ResolveNewAsync(A<string>._, A<IReadOnlySet<string>>._, A<CancellationToken>._))
            .Returns((ClinicalSourceRef?)null);
        A.CallTo(() => _mapper.Map(A<DocumentExtractionResult>._, A<string?>._))
            .Returns(new List<DerivedFact> { Fact() });
        IngestedDocument? persisted = null;
        A.CallTo(() => _store.AddAsync(A<IngestedDocument>._, A<CancellationToken>._))
            .Invokes((IngestedDocument d, CancellationToken _) => persisted = d);

        var result = await Service().IngestAsync(Request());

        result.Status.Should().Be(DocumentIngestionStatus.Ingested);
        result.CitationPending.Should().BeTrue();
        result.DocumentReferenceId.Should().BeNull();
        persisted!.OpenEmrDocumentReferenceId.Should().BeNull();
        A.CallTo(() => _mapper.Map(A<DocumentExtractionResult>._, null)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestAsync_WhenWriting_UsesTheConfiguredCategoryAndRequestFields()
    {
        await Service().IngestAsync(Request());

        A.CallTo(() => _writer.WriteAsync(
                A<DocumentWriteRequest>.That.Matches(r =>
                    r.PatientId == "p-1" && r.FileName == "lab.pdf" && r.CategoryPath == "AgentForge"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IngestAsync_OnSuccess_SnapshotsBeforeWriting()
    {
        await Service().IngestAsync(Request());

        A.CallTo(() => _resolver.SnapshotAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _writer.WriteAsync(A<DocumentWriteRequest>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }
}
