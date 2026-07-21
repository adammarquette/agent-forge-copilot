using FluentAssertions;
using MarqSpec.AgentForge.Agents.Ingestion;
using MarqSpec.AgentForge.Data.Entities;
using MarqSpec.AgentForge.Documents;
using MarqSpec.AgentForge.Llm;

namespace MarqSpec.AgentForge.UnitTests.Agents.Ingestion;

/// <summary>
/// Drives <see cref="DerivedFactMapper"/> — the per-schema citation shaping (W2_ARCHITECTURE.md §7). Each
/// extracted fact becomes one <see cref="DerivedFact"/> citing the OpenEMR DocumentReference, carrying the
/// page/quote/bbox from the extraction. A rejected extraction maps to nothing; a pending citation gets an
/// empty source id.
/// </summary>
public sealed class DerivedFactMapperTests
{
    private static readonly LlmUsage NoUsage = new(0, 0, 0m);

    private const string LabJson =
        """
        {"tests":[{"test_name":"Potassium","value":"5.8","unit":"mmol/L","reference_range":"3.5-5.1","collection_date":"2026-07-02","abnormal_flag":true,"citation":{"page":1,"quote":"K+ 5.8 (H)","bounding_box":[0.1,0.2,0.3,0.4]}}]}
        """;

    private const string IntakeJson =
        """
        {"demographics":{"full_name":"Jane Synthetic","date_of_birth":"1970-01-01","sex":"F"},"chief_concern":"palpitations","current_medications":[{"name":"Metoprolol","dose":"25mg","citation":{"page":2,"quote":"Metoprolol 25mg"}}],"allergies":["penicillin"],"family_history":["father MI at 60"],"citation":{"page":1,"quote":"intake form header"}}
        """;

    private static DocumentExtractionResult Lab(string json = LabJson) =>
        DocumentExtractionResult.Ok(ClinicalDocumentType.LabPdf, json, NoUsage);

    private static DocumentExtractionResult Intake(string json = IntakeJson) =>
        DocumentExtractionResult.Ok(ClinicalDocumentType.IntakeForm, json, NoUsage);

    [Fact]
    public void Map_WhenExtractionRejected_ReturnsEmpty()
    {
        var rejected = DocumentExtractionResult.Rejected(ClinicalDocumentType.LabPdf, "schema violation");

        new DerivedFactMapper().Map(rejected, "dr-1").Should().BeEmpty();
    }

    [Fact]
    public void Map_LabExtraction_ProducesOneFactPerTest_WithCitation()
    {
        var facts = new DerivedFactMapper().Map(Lab(), "dr-1");

        facts.Should().ContainSingle();
        var fact = facts[0];
        fact.FactType.Should().Be("lab.result");
        fact.PayloadJson.Should().Contain("Potassium").And.Contain("5.8");
        fact.Citation.SourceType.Should().Be(CitationSourceType.Derived);
        fact.Citation.SourceId.Should().Be("dr-1");
        fact.Citation.PageOrSection.Should().Be("1");
        fact.Citation.FieldOrChunkId.Should().Be("Potassium");
        fact.Citation.QuoteOrValue.Should().Be("K+ 5.8 (H)");
        fact.Citation.BoundingBox.Should().Equal(0.1, 0.2, 0.3, 0.4);
    }

    [Fact]
    public void Map_WhenExactQuoteLocated_SetsFullExtractionConfidence()
    {
        // A resolved bounding box means the extractor located the exact quote in the source PDF, so the
        // grounding/extraction confidence is full (FR-OBS-W2-1 per-encounter telemetry, gitlab#135).
        var facts = new DerivedFactMapper().Map(Lab(), "dr-1");

        facts[0].ExtractionConfidence.Should().Be(1.0);
    }

    [Fact]
    public void Map_WhenOnlyPageLevel_SetsReducedExtractionConfidence()
    {
        const string pageLevelOnly =
            """{"tests":[{"test_name":"Potassium","value":"5.8","unit":"mmol/L","reference_range":"3.5-5.1","collection_date":"2026-07-02","abnormal_flag":true,"citation":{"page":1,"quote":"K+ 5.8 (H)"}}]}""";

        var facts = new DerivedFactMapper().Map(Lab(pageLevelOnly), "dr-1");

        facts[0].ExtractionConfidence.Should().Be(0.5);
    }

    [Fact]
    public void Map_WhenCitationPending_SetsEmptySourceId()
    {
        var facts = new DerivedFactMapper().Map(Lab(), documentReferenceId: null);

        facts.Should().ContainSingle();
        facts[0].Citation.SourceId.Should().BeEmpty();
    }

    [Fact]
    public void Map_IntakeExtraction_ProducesFactsForEachSection()
    {
        var facts = new DerivedFactMapper().Map(Intake(), "dr-9");

        facts.Select(f => f.FactType).Should().BeEquivalentTo(
            "intake.demographics", "intake.chief_concern", "intake.medication", "intake.allergy", "intake.family_history");
        facts.Should().OnlyContain(f => f.Citation.SourceId == "dr-9");
    }

    [Fact]
    public void Map_IntakeMedication_UsesItsOwnCitationPageAndName()
    {
        var facts = new DerivedFactMapper().Map(Intake(), "dr-9");

        var medication = facts.Single(f => f.FactType == "intake.medication");
        medication.Citation.FieldOrChunkId.Should().Be("Metoprolol");
        medication.Citation.PageOrSection.Should().Be("2");
        medication.PayloadJson.Should().Contain("Metoprolol");
    }

    [Fact]
    public void Map_IntakeChiefConcern_WhenAbsent_IsOmitted()
    {
        const string noConcern =
            """
            {"demographics":{"full_name":"Jane Synthetic"},"current_medications":[],"allergies":[],"family_history":[],"citation":{"page":1,"quote":"header"}}
            """;

        var facts = new DerivedFactMapper().Map(Intake(noConcern), "dr-9");

        facts.Should().ContainSingle().Which.FactType.Should().Be("intake.demographics");
    }
}
