using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class ConditionMapperTests
{
    [Fact]
    public void MapBundle_ValidBundleWithOneCondition_ReturnsRecordWithSourceCitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "Condition",
                "id": "200",
                "code": { "text": "Atrial fibrillation" },
                "clinicalStatus": { "coding": [ { "code": "active" } ] },
                "onsetDateTime": "2024-03-15"
              }
            }
          ]
        }
        """;

        var records = ConditionMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("Condition/200");
        record.ProblemDisplay.Should().Be("Atrial fibrillation");
        record.ClinicalStatus.Should().Be("active");
        record.OnsetDate.Should().Be(new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void MapBundle_EmptyBundle_ReturnsEmptyList()
    {
        var records = ConditionMapper.MapBundle("""{ "resourceType": "Bundle" }""");

        records.Should().BeEmpty();
    }

    [Fact]
    public void MapBundle_MissingCodeText_FallsBackToCodingDisplay()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "Condition",
                "id": "201",
                "code": { "coding": [ { "display": "Heart failure with reduced ejection fraction" } ] }
              }
            }
          ]
        }
        """;

        var records = ConditionMapper.MapBundle(json);

        records.Should().ContainSingle().Which.ProblemDisplay
            .Should().Be("Heart failure with reduced ejection fraction");
    }

    [Fact]
    public void MapBundle_MissingClinicalStatus_ReturnsUnknownStatus()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Condition", "id": "202", "code": { "text": "CAD" } } }
          ]
        }
        """;

        var records = ConditionMapper.MapBundle(json);

        records.Should().ContainSingle().Which.ClinicalStatus.Should().Be("unknown");
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Condition", "code": { "text": "No id" } } },
            { "resource": { "resourceType": "Condition", "id": "203", "code": { "text": "Has id" } } }
          ]
        }
        """;

        var records = ConditionMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("203");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => ConditionMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
