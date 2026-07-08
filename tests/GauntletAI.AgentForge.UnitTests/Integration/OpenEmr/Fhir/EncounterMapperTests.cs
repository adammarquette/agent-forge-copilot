using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class EncounterMapperTests
{
    [Fact]
    public void MapBundle_ValidEncounter_ReturnsRecordWithSourceCitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "Encounter",
                "id": "500",
                "status": "finished",
                "type": [ { "text": "Office Visit" } ],
                "period": { "start": "2026-05-01T10:00:00Z", "end": "2026-05-01T10:30:00Z" },
                "reasonCode": [ { "text": "Follow-up AFib" } ]
              }
            }
          ]
        }
        """;

        var records = EncounterMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("Encounter/500");
        record.EncounterType.Should().Be("Office Visit");
        record.Status.Should().Be("finished");
        record.PeriodStart.Should().Be(new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero));
        record.PeriodEnd.Should().Be(new DateTimeOffset(2026, 5, 1, 10, 30, 0, TimeSpan.Zero));
        record.ReasonDisplay.Should().Be("Follow-up AFib");
    }

    [Fact]
    public void MapBundle_MissingReasonCodeAndPeriodEnd_ReturnsNullForBoth()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "Encounter",
                "id": "501",
                "status": "in-progress",
                "type": [ { "text": "ED Visit" } ],
                "period": { "start": "2026-05-02T08:00:00Z" }
              }
            }
          ]
        }
        """;

        var records = EncounterMapper.MapBundle(json);

        var record = records.Should().ContainSingle().Subject;
        record.ReasonDisplay.Should().BeNull();
        record.PeriodEnd.Should().BeNull();
    }

    [Fact]
    public void MapBundle_MissingType_ReturnsUnknownEncounterType()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Encounter", "id": "502", "status": "finished" } }
          ]
        }
        """;

        var records = EncounterMapper.MapBundle(json);

        records.Should().ContainSingle().Which.EncounterType.Should().Be("Unknown encounter type");
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Encounter", "status": "finished" } },
            { "resource": { "resourceType": "Encounter", "id": "503", "status": "finished" } }
          ]
        }
        """;

        var records = EncounterMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("503");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => EncounterMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
