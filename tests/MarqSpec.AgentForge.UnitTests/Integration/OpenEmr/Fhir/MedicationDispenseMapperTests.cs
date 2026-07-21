using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class MedicationDispenseMapperTests
{
    [Fact]
    public void MapBundle_ValidDispense_ReturnsRecordWithSourceCitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "MedicationDispense",
                "id": "700",
                "status": "completed",
                "medicationCodeableConcept": { "text": "Metoprolol Tartrate 50mg" },
                "whenHandedOver": "2026-05-15",
                "daysSupply": { "value": 30 }
              }
            }
          ]
        }
        """;

        var records = MedicationDispenseMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("MedicationDispense/700");
        record.MedicationDisplay.Should().Be("Metoprolol Tartrate 50mg");
        record.Status.Should().Be("completed");
        record.WhenHandedOver.Should().Be(new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero));
        record.DaysSupply.Should().Be(30);
    }

    [Fact]
    public void MapBundle_MissingDaysSupplyAndWhenHandedOver_ReturnsNullForBoth()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "MedicationDispense",
                "id": "701",
                "status": "completed",
                "medicationCodeableConcept": { "text": "Warfarin 5mg" }
              }
            }
          ]
        }
        """;

        var records = MedicationDispenseMapper.MapBundle(json);

        var record = records.Should().ContainSingle().Subject;
        record.WhenHandedOver.Should().BeNull();
        record.DaysSupply.Should().BeNull();
    }

    [Fact]
    public void MapBundle_MissingMedicationCodeableConcept_FallsBackToCodingDisplay()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "MedicationDispense",
                "id": "702",
                "status": "completed",
                "medicationCodeableConcept": { "coding": [ { "display": "Atorvastatin 20mg" } ] }
              }
            }
          ]
        }
        """;

        var records = MedicationDispenseMapper.MapBundle(json);

        records.Should().ContainSingle().Which.MedicationDisplay.Should().Be("Atorvastatin 20mg");
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "MedicationDispense", "status": "completed", "medicationCodeableConcept": { "text": "No id" } } },
            { "resource": { "resourceType": "MedicationDispense", "id": "703", "status": "completed", "medicationCodeableConcept": { "text": "Has id" } } }
          ]
        }
        """;

        var records = MedicationDispenseMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("703");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => MedicationDispenseMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
