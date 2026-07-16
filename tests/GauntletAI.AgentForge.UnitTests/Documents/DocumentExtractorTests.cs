using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Documents;

/// <summary>
/// Unit tests for <see cref="DocumentExtractor"/>. The guarded failure mode is the trust core: raw model
/// output that does not satisfy the strict schema must be rejected, not persisted (W2_ARCHITECTURE.md §3).
/// </summary>
public sealed class DocumentExtractorTests
{
    private readonly ILlmProvider _llm = A.Fake<ILlmProvider>();
    private readonly IPdfWordReader _pdfReader = A.Fake<IPdfWordReader>();

    private DocumentExtractor CreateSut()
    {
        // Default: no PDF geometry, so citation boxes are left as the model gave them. Tests that exercise
        // box resolution override this after construction (last FakeItEasy config wins).
        A.CallTo(() => _pdfReader.ReadWords(A<ReadOnlyMemory<byte>>._)).Returns((IReadOnlyList<PdfWord>)[]);
        return new(_llm, _pdfReader, NullLogger<DocumentExtractor>.Instance);
    }

    private void SetupModelReturns(string content) =>
        A.CallTo(() => _llm.CompleteAsync(A<LlmRequest>._, A<CancellationToken>._))
            .Returns(new LlmResponse(content, [], LlmStopReason.EndTurn, new LlmUsage(0, 0, 0m)));

    [Fact]
    public async Task ExtractAsync_WhenModelReturnsValidLabJson_Succeeds()
    {
        SetupModelReturns(
            """
            {"tests":[{"test_name":"INR","value":"2.5","unit":null,"reference_range":"2.0-3.0",
            "collection_date":"2026-07-01","abnormal_flag":false,
            "citation":{"page":1,"quote":"INR 2.5","bounding_box":null}}]}
            """);

        var result = await CreateSut().ExtractAsync(
            ClinicalDocumentType.LabPdf, new byte[] { 1, 2, 3 }, "application/pdf", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.CanonicalJson.Should().NotBeNull().And.Contain("INR");
    }

    [Fact]
    public async Task ExtractAsync_WhenLabResultOmitsRequiredCitation_RejectsAndPersistsNothing()
    {
        // The required 'citation' is missing - unschematized VLM output must not pass the gate.
        SetupModelReturns("""{"tests":[{"test_name":"INR","value":"2.5"}]}""");

        var result = await CreateSut().ExtractAsync(
            ClinicalDocumentType.LabPdf, new byte[] { 1 }, "application/pdf", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.CanonicalJson.Should().BeNull();
        result.RejectionReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractAsync_WhenPdfWordsLocateTheQuote_ReplacesTheEstimateWithTheExactBox()
    {
        var sut = CreateSut();
        A.CallTo(() => _pdfReader.ReadWords(A<ReadOnlyMemory<byte>>._)).Returns((IReadOnlyList<PdfWord>)
        [
            new(1, "INR", 0.10, 0.20, 0.06, 0.03),
            new(1, "2.5", 0.20, 0.20, 0.05, 0.03),
        ]);
        SetupModelReturns(
            """
            {"tests":[{"test_name":"INR","value":"2.5","unit":null,"reference_range":null,
            "collection_date":null,"abnormal_flag":null,
            "citation":{"page":1,"quote":"INR 2.5","bounding_box":null}}]}
            """);

        var result = await sut.ExtractAsync(
            ClinicalDocumentType.LabPdf, new byte[] { 1, 2, 3 }, "application/pdf", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        // The model's null box is replaced by the union of the located words (top-left corner at 0.10, 0.20).
        result.CanonicalJson.Should().NotContain("\"bounding_box\":null").And.Contain("\"bounding_box\":[0.1,0.2,");
    }

    [Fact]
    public async Task ExtractAsync_WhenModelReturnsNoJson_Rejects()
    {
        SetupModelReturns("I'm sorry, I could not read the document.");

        var result = await CreateSut().ExtractAsync(
            ClinicalDocumentType.IntakeForm, new byte[] { 1 }, "application/pdf", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.RejectionReason.Should().NotBeNullOrEmpty();
    }
}
