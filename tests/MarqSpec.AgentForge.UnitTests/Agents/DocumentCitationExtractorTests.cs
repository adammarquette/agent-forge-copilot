using FluentAssertions;
using MarqSpec.AgentForge.Agents;
using MarqSpec.AgentForge.Data.Entities;

namespace MarqSpec.AgentForge.UnitTests.Agents;

/// <summary>
/// Unit tests for <see cref="DocumentCitationExtractor"/> — the read-side of the click-to-source overlay
/// (FR-CITE-2, gitlab#96). The extraction schema carries a per-fact page + bounding box, but the answer-path
/// projections drop it; this surfaces it structurally so the client can highlight the region. Guarded
/// behavior: one citation per cited fact with page/bbox/quote; a fact whose bbox is absent degrades to
/// page-level (null bbox, page kept); malformed or empty input yields no citations rather than throwing.
/// </summary>
public sealed class DocumentCitationExtractorTests
{
    private const string LabJson = """
    {
      "tests": [
        { "test_name": "INR", "value": "3.4", "reference_range": "2.0-3.0", "abnormal_flag": true,
          "citation": { "page": 1, "quote": "INR 3.4 (H)", "bounding_box": [0.12, 0.34, 0.4, 0.05] } },
        { "test_name": "Potassium", "value": "5.9", "unit": "mmol/L",
          "citation": { "page": 2, "quote": "K 5.9" } }
      ]
    }
    """;

    [Fact]
    public void Extract_LabPdf_ReturnsOneCitationPerTest_WithPageBboxAndQuote()
    {
        var citations = DocumentCitationExtractor.Extract(LabJson, ClinicalDocumentType.LabPdf);

        citations.Should().HaveCount(2);
        var inr = citations[0];
        inr.FactId.Should().Be("INR");
        inr.Field.Should().Be("INR");
        inr.Value.Should().Be("3.4");
        inr.Page.Should().Be(1);
        inr.BoundingBox.Should().Equal(0.12, 0.34, 0.4, 0.05);
        inr.Quote.Should().Be("INR 3.4 (H)");
    }

    [Fact]
    public void Extract_LabPdf_WhenBboxAbsent_DegradesToPageLevel()
    {
        var citations = DocumentCitationExtractor.Extract(LabJson, ClinicalDocumentType.LabPdf);

        // "Potassium" has a citation with a page but no bounding_box: keep the page, null the bbox.
        var potassium = citations[1];
        potassium.FactId.Should().Be("Potassium");
        potassium.Page.Should().Be(2);
        potassium.BoundingBox.Should().BeNull();
        potassium.Quote.Should().Be("K 5.9");
    }

    [Fact]
    public void Extract_FactIdMatchesTheAnswerCitationSlug()
    {
        // The answer text cites document facts as [Lab/<slug>] where slug is the whitespace-free test name;
        // the overlay ties a clicked token to its region by that same slug, so the id must match.
        var citations = DocumentCitationExtractor.Extract(
            """{ "tests": [ { "test_name": "LDL Cholesterol", "value": "160", "citation": { "page": 1, "quote": "LDL 160" } } ] }""",
            ClinicalDocumentType.LabPdf);

        citations.Should().ContainSingle().Which.FactId.Should().Be("LDLCholesterol");
    }

    [Fact]
    public void Extract_TestMissingCitation_IsSkipped()
    {
        var citations = DocumentCitationExtractor.Extract(
            """{ "tests": [ { "test_name": "INR", "value": "3.4" } ] }""",
            ClinicalDocumentType.LabPdf);

        citations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all {")]
    [InlineData("""{ "unexpected": "shape" }""")]
    public void Extract_NullEmptyOrMalformed_ReturnsEmptyWithoutThrowing(string? factsJson)
    {
        var citations = DocumentCitationExtractor.Extract(factsJson, ClinicalDocumentType.LabPdf);

        citations.Should().BeEmpty();
    }
}
