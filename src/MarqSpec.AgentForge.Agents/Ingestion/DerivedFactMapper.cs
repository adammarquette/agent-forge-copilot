using System.Globalization;
using System.Text.Json;
using MarqSpec.AgentForge.Data.Entities;
using MarqSpec.AgentForge.Documents;
using MarqSpec.AgentForge.Documents.Extraction;

namespace MarqSpec.AgentForge.Agents.Ingestion;

/// <inheritdoc />
public sealed class DerivedFactMapper : IDerivedFactMapper
{
    /// <inheritdoc />
    public IReadOnlyList<DerivedFact> Map(DocumentExtractionResult extraction, string? documentReferenceId)
    {
        if (!extraction.Succeeded || extraction.CanonicalJson is not { } json)
        {
            return [];
        }

        // Empty string (not null) marks a pending citation — Citation.SourceId is a required column.
        var sourceId = documentReferenceId ?? string.Empty;
        return extraction.DocumentType switch
        {
            ClinicalDocumentType.LabPdf => MapLab(json, sourceId),
            ClinicalDocumentType.IntakeForm => MapIntake(json, sourceId),
            _ => [],
        };
    }

    private static List<DerivedFact> MapLab(string json, string sourceId)
    {
        var lab = JsonSerializer.Deserialize(json, DerivedFactJsonContext.Default.LabExtraction)
            ?? throw new JsonException("Canonical lab extraction did not deserialize.");

        var facts = new List<DerivedFact>(lab.Tests.Count);
        foreach (var test in lab.Tests)
        {
            facts.Add(new DerivedFact
            {
                FactType = "lab.result",
                PayloadJson = JsonSerializer.Serialize(test, DerivedFactJsonContext.Default.LabTestResult),
                Citation = Cite(sourceId, test.Citation, test.TestName, test.Citation.Quote),
                ExtractionConfidence = ConfidenceFrom(test.Citation),
            });
        }

        return facts;
    }

    private static List<DerivedFact> MapIntake(string json, string sourceId)
    {
        var intake = JsonSerializer.Deserialize(json, DerivedFactJsonContext.Default.IntakeExtraction)
            ?? throw new JsonException("Canonical intake extraction did not deserialize.");

        var facts = new List<DerivedFact>
        {
            new()
            {
                FactType = "intake.demographics",
                PayloadJson = JsonSerializer.Serialize(intake.Demographics, DerivedFactJsonContext.Default.IntakeDemographics),
                Citation = Cite(sourceId, intake.Citation, "demographics", intake.Citation.Quote),
                ExtractionConfidence = ConfidenceFrom(intake.Citation),
            },
        };

        if (!string.IsNullOrWhiteSpace(intake.ChiefConcern))
        {
            facts.Add(TextFact("intake.chief_concern", "chief_concern", intake.ChiefConcern, sourceId, intake.Citation));
        }

        foreach (var medication in intake.CurrentMedications)
        {
            facts.Add(new DerivedFact
            {
                FactType = "intake.medication",
                PayloadJson = JsonSerializer.Serialize(medication, DerivedFactJsonContext.Default.IntakeMedication),
                Citation = Cite(sourceId, medication.Citation, medication.Name, medication.Citation.Quote),
                ExtractionConfidence = ConfidenceFrom(medication.Citation),
            });
        }

        foreach (var allergy in intake.Allergies)
        {
            facts.Add(TextFact("intake.allergy", "allergy", allergy, sourceId, intake.Citation));
        }

        foreach (var item in intake.FamilyHistory)
        {
            facts.Add(TextFact("intake.family_history", "family_history", item, sourceId, intake.Citation));
        }

        return facts;
    }

    // Free-text intake items (chief concern, allergy, family history) share the form-level citation and carry
    // the item text as both the payload and the quoted value.
    private static DerivedFact TextFact(
        string factType, string field, string value, string sourceId, ExtractionCitation citation) =>
        new()
        {
            FactType = factType,
            PayloadJson = JsonSerializer.Serialize(new TextFactPayload(value), DerivedFactJsonContext.Default.TextFactPayload),
            Citation = Cite(sourceId, citation, field, value),
            ExtractionConfidence = ConfidenceFrom(citation),
        };

    // Grounding/locatability confidence for a derived fact: 1.0 when the extractor located the exact quote in
    // the source PDF (a resolved bounding box), else 0.5 (page-level only). reference: gitlab#135 (FR-OBS-W2-1).
    private static double ConfidenceFrom(ExtractionCitation citation) => citation.BoundingBox is not null ? 1.0 : 0.5;

    private static Citation Cite(string sourceId, ExtractionCitation citation, string field, string? quote) => new()
    {
        SourceType = CitationSourceType.Derived,
        SourceId = sourceId,
        PageOrSection = citation.Page.ToString(CultureInfo.InvariantCulture),
        FieldOrChunkId = field,
        QuoteOrValue = quote,
        BoundingBox = citation.BoundingBox,
    };
}
