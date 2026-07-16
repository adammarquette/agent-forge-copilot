using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using GauntletAI.AgentForge.UnitTests.TestSupport;

namespace GauntletAI.AgentForge.UnitTests.Agent;

public sealed class DocumentFactsToolTests
{
    private readonly IDerivedFactStore _factStore = A.Fake<IDerivedFactStore>();
    private readonly IClinicianIdentityAccessor _clinician = A.Fake<IClinicianIdentityAccessor>();
    private readonly ICorrelationIdAccessor _correlation = A.Fake<ICorrelationIdAccessor>();
    private readonly CapturingLogger<DocumentFactsTool> _logger = new();
    private readonly DocumentFactsTool _sut;

    public DocumentFactsToolTests()
    {
        A.CallTo(() => _clinician.ClinicianIdentity).Returns("dr-demo");
        A.CallTo(() => _correlation.CorrelationId).Returns("corr-1");
        _sut = new DocumentFactsTool(_factStore, _clinician, _correlation, _logger);
    }

    [Fact]
    public async Task GetAsync_FactsOnFile_ProjectsCitableDocumentRecordsPreferringTheOpenEmrSourceId()
    {
        var facts = new List<DerivedFact>
        {
            Fact("11111111-0000-0000-0000-000000000000", "lab.result", "Potassium 5.9 (H) mmol/L", docRef: "docref-1", sourceId: "cite-1", page: "2"),
            Fact("22222222-0000-0000-0000-000000000000", "intake.medication", "Metoprolol 100 mg", docRef: null, sourceId: "cite-2", page: null),
        };
        A.CallTo(() => _factStore.GetByPatientAsync("pat-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<DerivedFact>>(facts));

        var result = await _sut.GetAsync("pat-1", CancellationToken.None);

        result.Facts.Should().HaveCount(2);
        result.Facts[0].ResourceType.Should().Be("Document");
        result.Facts[0].Id.Should().Be("11111111");
        result.Facts[0].SourceDocumentId.Should().Be("docref-1");   // the OpenEMR docref is preferred
        result.Facts[0].Value.Should().Be("Potassium 5.9 (H) mmol/L");
        result.Facts[0].Page.Should().Be("2");
        result.Facts[1].SourceDocumentId.Should().Be("cite-2");     // falls back to the citation's own SourceId
    }

    [Fact]
    public async Task GetAsync_FactWithNoValueOrNoSourceDocument_SkipsItRatherThanEmittingABrokenTarget()
    {
        var facts = new List<DerivedFact>
        {
            Fact("33333333-0000-0000-0000-000000000000", "lab.result", value: null, docRef: "docref-3", sourceId: "cite-3", page: null),
            Fact("44444444-0000-0000-0000-000000000000", "lab.result", "Sodium 139", docRef: null, sourceId: "", page: null),
            Fact("55555555-0000-0000-0000-000000000000", "lab.result", "Creatinine 1.1", docRef: "docref-5", sourceId: "cite-5", page: "1"),
        };
        A.CallTo(() => _factStore.GetByPatientAsync("pat-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<DerivedFact>>(facts));

        var result = await _sut.GetAsync("pat-1", CancellationToken.None);

        result.Facts.Should().ContainSingle().Which.Id.Should().Be("55555555");
    }

    [Fact]
    public async Task GetAsync_Always_AuditsTheAccessUnderTheClinicianIdentityAndTool()
    {
        A.CallTo(() => _factStore.GetByPatientAsync("pat-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<DerivedFact>>([]));

        await _sut.GetAsync("pat-1", CancellationToken.None);

        _logger.Lines.Should().ContainSingle(line =>
            line.Contains("ACCESS AUDIT", StringComparison.Ordinal)
            && line.Contains("dr-demo", StringComparison.Ordinal)
            && line.Contains("get_document_facts", StringComparison.Ordinal));
    }

    private static DerivedFact Fact(string id, string factType, string? value, string? docRef, string sourceId, string? page) => new()
    {
        Id = Guid.Parse(id),
        FactType = factType,
        PayloadJson = "{}",
        Citation = new Citation { SourceId = sourceId, QuoteOrValue = value, PageOrSection = page },
        Document = docRef is null
            ? null
            : new IngestedDocument { PatientId = "pat-1", ContentHash = "hash-" + id[..8], OpenEmrDocumentReferenceId = docRef },
    };
}
