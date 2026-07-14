using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agents;
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
    private readonly ILlmProvider _llm = A.Fake<ILlmProvider>();
    private readonly IClinicalResponseVerifier _verifier = A.Fake<IClinicalResponseVerifier>();

    private EvidenceAgentSupervisor CreateSut()
    {
        IReadOnlyList<EvidenceSnippet> noEvidence = [];
        A.CallTo(() => _retriever.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._)).Returns(noEvidence);
        A.CallTo(() => _llm.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(new LlmResponse("draft answer", [], LlmStopReason.EndTurn, new LlmUsage(0, 0, 0m)));
        A.CallTo(() => _verifier.Verify(A<string>._, A<IReadOnlyCollection<string>>._))
            .Returns(new VerificationResult(true, "verified answer", [], []));
        return new EvidenceAgentSupervisor(
            _extractor, _retriever, _llm, _verifier, NullLogger<EvidenceAgentSupervisor>.Instance);
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
}
