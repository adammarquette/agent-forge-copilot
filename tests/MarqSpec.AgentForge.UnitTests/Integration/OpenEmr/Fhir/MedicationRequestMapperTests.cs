using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class MedicationRequestMapperTests
{
    [Fact]
    public void MapBundle_ValidBundleWithOneMedicationRequest_ReturnsRecordWithSourceCitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "type": "searchset",
          "entry": [
            {
              "resource": {
                "resourceType": "MedicationRequest",
                "id": "123",
                "status": "active",
                "medicationCodeableConcept": { "text": "Metoprolol Tartrate 50mg" },
                "dosageInstruction": [ { "text": "Take 1 tablet by mouth twice daily" } ],
                "authoredOn": "2026-05-01"
              }
            }
          ]
        }
        """;

        var records = MedicationRequestMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("MedicationRequest/123");
        record.MedicationDisplay.Should().Be("Metoprolol Tartrate 50mg");
        record.Dosage.Should().Be("Take 1 tablet by mouth twice daily");
        record.Status.Should().Be("active");
        record.AuthoredOn.Should().Be(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void MapBundle_EmptyBundle_ReturnsEmptyList()
    {
        const string json = """{ "resourceType": "Bundle", "type": "searchset" }""";

        var records = MedicationRequestMapper.MapBundle(json);

        records.Should().BeEmpty();
    }

    [Fact]
    public void MapBundle_BundleWithNonMedicationRequestEntry_IgnoresIt()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Patient", "id": "1" } }
          ]
        }
        """;

        var records = MedicationRequestMapper.MapBundle(json);

        records.Should().BeEmpty();
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
                "resourceType": "MedicationRequest",
                "id": "124",
                "status": "active",
                "medicationCodeableConcept": {
                  "coding": [ { "display": "Lisinopril 10mg" } ]
                }
              }
            }
          ]
        }
        """;

        var records = MedicationRequestMapper.MapBundle(json);

        records.Should().ContainSingle().Which.MedicationDisplay.Should().Be("Lisinopril 10mg");
    }

    [Fact]
    public void MapBundle_NoMedicationTextOrCodingAvailable_UsesUnknownMedicationPlaceholder()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "MedicationRequest",
                "id": "125",
                "status": "active"
              }
            }
          ]
        }
        """;

        var records = MedicationRequestMapper.MapBundle(json);

        records.Should().ContainSingle().Which.MedicationDisplay.Should().Be(MedicationRequestMapper.UnknownMedicationDisplay);
    }

    [Fact]
    public void MapBundle_MissingDosageInstruction_ReturnsNullDosage()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "MedicationRequest",
                "id": "126",
                "status": "active",
                "medicationCodeableConcept": { "text": "Atorvastatin 20mg" }
              }
            }
          ]
        }
        """;

        var records = MedicationRequestMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Dosage.Should().BeNull();
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "MedicationRequest",
                "status": "active",
                "medicationCodeableConcept": { "text": "No id, can't cite this" }
              }
            },
            {
              "resource": {
                "resourceType": "MedicationRequest",
                "id": "127",
                "status": "active",
                "medicationCodeableConcept": { "text": "Has an id" }
              }
            }
          ]
        }
        """;

        var records = MedicationRequestMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("127");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        const string json = "{ not valid json";

        var act = () => MedicationRequestMapper.MapBundle(json);

        act.Should().Throw<FhirParsingException>();
    }
}
