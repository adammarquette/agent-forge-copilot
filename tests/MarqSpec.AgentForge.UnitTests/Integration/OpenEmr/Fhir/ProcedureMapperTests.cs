using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class ProcedureMapperTests
{
    [Fact]
    public void MapBundle_ValidProcedure_ReturnsRecordWithSourceCitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "Procedure",
                "id": "600",
                "status": "completed",
                "code": { "text": "Percutaneous coronary intervention" },
                "performedDateTime": "2026-03-10"
              }
            }
          ]
        }
        """;

        var records = ProcedureMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("Procedure/600");
        record.ProcedureDisplay.Should().Be("Percutaneous coronary intervention");
        record.Status.Should().Be("completed");
        record.PerformedDate.Should().Be(new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void MapBundle_MissingPerformedDateTime_ReturnsNull()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Procedure", "id": "601", "status": "completed", "code": { "text": "Ablation" } } }
          ]
        }
        """;

        var records = ProcedureMapper.MapBundle(json);

        records.Should().ContainSingle().Which.PerformedDate.Should().BeNull();
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
                "resourceType": "Procedure",
                "id": "602",
                "status": "completed",
                "code": { "coding": [ { "display": "Cardiac catheterization" } ] }
              }
            }
          ]
        }
        """;

        var records = ProcedureMapper.MapBundle(json);

        records.Should().ContainSingle().Which.ProcedureDisplay.Should().Be("Cardiac catheterization");
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Procedure", "status": "completed", "code": { "text": "No id" } } },
            { "resource": { "resourceType": "Procedure", "id": "603", "status": "completed", "code": { "text": "Has id" } } }
          ]
        }
        """;

        var records = ProcedureMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("603");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => ProcedureMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
