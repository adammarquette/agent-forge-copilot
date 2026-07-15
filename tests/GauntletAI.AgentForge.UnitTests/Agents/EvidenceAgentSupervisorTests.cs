using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Agents;

/// <summary>
/// Unit tests for <see cref="EvidenceAgentSupervisor"/>. Guarded behavior: the graph routes through explicit,
/// ordered handoffs, returns the critic's verified answer, and degrades (does not crash) when a worker fails.
/// </summary>
public sealed class EvidenceAgentSupervisorTests
{
    private readonly IDocumentExtractor _extractor = A.Fake<IDocumentExtractor>();
    private readonly IEvidenceRetriever _retriever = A.Fake<IEvidenceRetriever>();
    private readonly IDerivedFactStore _factStore = A.Fake<IDerivedFactStore>();
    private readonly ILlmProvider _llm = A.Fake<ILlmProvider>();
    private readonly IClinicalResponseVerifier _verifier = A.Fake<IClinicalResponseVerifier>();

    private EvidenceAgentSupervisor CreateSut()
    {
        IReadOnlyList<EvidenceSnippet> noEvidence = [];
        A.CallTo(() => _retriever.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns(noEvidence);
        IReadOnlyList<DerivedFact> noFacts = [];
        A.CallTo(() => _factStore.GetByPatientAsync(A<string>._, A<CancellationToken>._)).Returns(noFacts);
        A.CallTo(() => _llm.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(new LlmResponse("draft answer", [], LlmStopReason.EndTurn, new LlmUsage(0, 0, 0m)));
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Returns(new VerificationResult(true, "verified answer", [], []));
        return new EvidenceAgentSupervisor(
            _extractor, _retriever, _factStore, _llm, _verifier, NullLogger<EvidenceAgentSupervisor>.Instance);
    }

    private static PendingDocument LabPdf() =>
        new(ClinicalDocumentType.LabPdf, new byte[] { 1 }, "application/pdf");

    [Fact]
    public async Task RunAsync_WithDocument_RoutesThroughAllNodesAndReturnsVerifiedAnswer()
    {
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Ok(ClinicalDocumentType.LabPdf, "{\"tests\":[]}", new LlmUsage(0, 0, 0m)));
        var request = new EvidenceAgentRequest { PatientId = "p1", Question = "Is her INR therapeutic?", Document = LabPdf() };

        var result = await CreateSut().RunAsync(request, CancellationToken.None);

        result.Answer.Should().Be("verified answer");
        result.Handoffs.Select(h => h.To).Should()
            .Contain(["intake-extractor", "evidence-retriever", "answer-composer", "critic"]);
    }

    [Fact]
    public async Task RunAsync_WithoutDocument_SkipsExtractionButStillVerifies()
    {
        var request = new EvidenceAgentRequest { PatientId = "p1", Question = "What changed?" };

        var result = await CreateSut().RunAsync(request, CancellationToken.None);

        result.Answer.Should().Be("verified answer");
        result.Handoffs.Select(h => h.To).Should().Contain("critic").And.NotContain("intake-extractor");
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task RunAsync_WhenExtractionRejected_DegradesAndStillAnswers()
    {
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Rejected(ClinicalDocumentType.LabPdf, "schema failed"));
        var request = new EvidenceAgentRequest { PatientId = "p1", Question = "q", Document = LabPdf() };

        var result = await CreateSut().RunAsync(request, CancellationToken.None);

        result.ExtractedFactsJson.Should().BeNull();
        result.Answer.Should().Be("verified answer");
    }

    [Fact]
    public async Task RunAsync_WithLabFacts_HandsLabValuesToCriticAsResolvableCitations()
    {
        // Regression (document-upload polish): extracted patient labs must be projected to the
        // scanner's citation shape (ResourceType "Lab" + Id), so a [Lab/INR] citation in the answer
        // resolves instead of being suppressed as an uncited clinical value.
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Ok(
                ClinicalDocumentType.LabPdf,
                """{"tests":[{"test_name":"INR","value":"3.8","unit":"ratio","reference_range":"2.0 - 3.0","abnormal_flag":true}]}""",
                new LlmUsage(0, 0, 0m)));
        var sut = CreateSut();
        IReadOnlyCollection<string>? toolResults = null;
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Invokes((string _, IReadOnlyCollection<string> tr) => toolResults = tr)
            .Returns(new VerificationResult(true, "verified answer", [], []));
        var request = new EvidenceAgentRequest { PatientId = "p1", Question = "Is her INR therapeutic?", Document = LabPdf() };

        await sut.RunAsync(request, CancellationToken.None);

        toolResults.Should().NotBeNull();
        toolResults!.Any(t => t.Contains("\"ResourceType\":\"Lab\"") && t.Contains("\"Id\":\"INR\"")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_MultiWordLabName_ProducesWhitespaceFreeCitationId()
    {
        // The attribution regex only accepts [A-Za-z0-9-.] in the citation id, so a multi-word test
        // name ("LDL Cholesterol") must slugify to a space-free id or its [Lab/...] citation cannot parse.
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Ok(
                ClinicalDocumentType.LabPdf,
                """{"tests":[{"test_name":"LDL Cholesterol","value":"145"}]}""",
                new LlmUsage(0, 0, 0m)));
        var sut = CreateSut();
        IReadOnlyCollection<string>? toolResults = null;
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Invokes((string _, IReadOnlyCollection<string> tr) => toolResults = tr)
            .Returns(new VerificationResult(true, "verified answer", [], []));
        var request = new EvidenceAgentRequest { PatientId = "p1", Question = "Is the LDL at goal?", Document = LabPdf() };

        await sut.RunAsync(request, CancellationToken.None);

        toolResults!.Any(t => t.Contains("\"Id\":\"LDLCholesterol\"")).Should().BeTrue();
        // The citation id itself must never carry the space (TestName legitimately keeps the original).
        toolResults!.Any(t => t.Contains("\"Id\":\"LDL Cholesterol\"")).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithLabFacts_PresentsLabCitationTokenToComposer()
    {
        A.CallTo(() => _extractor.ExtractAsync(
                A<ClinicalDocumentType>._, A<ReadOnlyMemory<byte>>._, A<string>._, A<CancellationToken>._))
            .Returns(DocumentExtractionResult.Ok(
                ClinicalDocumentType.LabPdf,
                """{"tests":[{"test_name":"INR","value":"3.8","unit":"ratio"}]}""",
                new LlmUsage(0, 0, 0m)));
        var sut = CreateSut();
        LlmRequest? captured = null;
        A.CallTo(() => _llm.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest r, CancellationToken _) => captured = r)
            .Returns(new LlmResponse("draft answer", [], LlmStopReason.EndTurn, new LlmUsage(0, 0, 0m)));
        var request = new EvidenceAgentRequest { PatientId = "p1", Question = "Is her INR therapeutic?", Document = LabPdf() };

        await sut.RunAsync(request, CancellationToken.None);

        var composerText = string.Concat(captured!.Messages
            .SelectMany(m => m.Content).OfType<LlmTextContent>().Select(c => c.Text));
        composerText.Should().Contain("[Lab/INR]");
    }

    [Fact]
    public async Task RunAsync_SurfacesPreIngestedDerivedFacts_ToComposerAndCritic()
    {
        // The read side of E2 (UC-6): facts ingested pre-visit must reach the brief even with NO document
        // attached this turn - surfaced to the composer as a [Derived/<slug>] token and made resolvable to
        // the critic (ResourceType "Derived") so the value isn't suppressed as an uncited claim.
        var fact = new DerivedFact
        {
            Id = Guid.NewGuid(),
            FactType = "lab.result",
            PayloadJson = "{}",
            Citation = new Citation
            {
                SourceType = CitationSourceType.Derived,
                SourceId = "docref-1",
                PageOrSection = "1",
                QuoteOrValue = "Potassium 5.9 (H) mmol/L",
            },
            Document = new IngestedDocument
            {
                PatientId = "p1",
                ContentHash = "hash-1",
                OpenEmrDocumentReferenceId = "docref-1",
            },
        };
        var sut = CreateSut();
        IReadOnlyList<DerivedFact> facts = [fact];
        A.CallTo(() => _factStore.GetByPatientAsync("p1", A<CancellationToken>._)).Returns(facts);
        LlmRequest? captured = null;
        A.CallTo(() => _llm.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Invokes((LlmRequest r, CancellationToken _) => captured = r)
            .Returns(new LlmResponse("draft answer", [], LlmStopReason.EndTurn, new LlmUsage(0, 0, 0m)));
        IReadOnlyCollection<string>? toolResults = null;
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Invokes((string _, IReadOnlyCollection<string> tr) => toolResults = tr)
            .Returns(new VerificationResult(true, "verified answer", [], []));
        var request = new EvidenceAgentRequest { PatientId = "p1", Question = "What changed?" };

        await sut.RunAsync(request, CancellationToken.None);

        var composerText = string.Concat(captured!.Messages
            .SelectMany(m => m.Content).OfType<LlmTextContent>().Select(c => c.Text));
        composerText.Should().Contain("[Derived/").And.Contain("Potassium 5.9 (H) mmol/L");
        toolResults!.Any(t => t.Contains("\"ResourceType\":\"Derived\"")).Should().BeTrue();
    }
}
