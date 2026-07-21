using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class AllergyIntoleranceMapperTests
{
    [Fact]
    public void MapBundle_ValidAllergy_ReturnsRecordWithSourceCitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "AllergyIntolerance",
                "id": "400",
                "code": { "text": "Penicillin" },
                "clinicalStatus": { "coding": [ { "code": "active" } ] },
                "criticality": "high",
                "reaction": [ { "manifestation": [ { "text": "Hives" } ] } ]
              }
            }
          ]
        }
        """;

        var records = AllergyIntoleranceMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("AllergyIntolerance/400");
        record.AllergenDisplay.Should().Be("Penicillin");
        record.ClinicalStatus.Should().Be("active");
        record.Criticality.Should().Be("high");
        record.ReactionManifestation.Should().Be("Hives");
    }

    [Fact]
    public void MapBundle_MissingCriticalityAndReaction_ReturnsNullForBoth()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "AllergyIntolerance", "id": "401", "code": { "text": "Sulfa" } } }
          ]
        }
        """;

        var records = AllergyIntoleranceMapper.MapBundle(json);

        var record = records.Should().ContainSingle().Subject;
        record.Criticality.Should().BeNull();
        record.ReactionManifestation.Should().BeNull();
    }

    [Fact]
    public void MapBundle_CodeTextMissing_FallsBackToCodingDisplay()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "AllergyIntolerance",
                "id": "402",
                "code": { "coding": [ { "display": "Latex" } ] }
              }
            }
          ]
        }
        """;

        var records = AllergyIntoleranceMapper.MapBundle(json);

        records.Should().ContainSingle().Which.AllergenDisplay.Should().Be("Latex");
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "AllergyIntolerance", "code": { "text": "No id" } } },
            { "resource": { "resourceType": "AllergyIntolerance", "id": "403", "code": { "text": "Has id" } } }
          ]
        }
        """;

        var records = AllergyIntoleranceMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("403");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => AllergyIntoleranceMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
