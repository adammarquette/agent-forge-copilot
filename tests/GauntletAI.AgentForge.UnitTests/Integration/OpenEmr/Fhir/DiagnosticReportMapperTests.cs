using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class DiagnosticReportMapperTests
{
    [Fact]
    public void MapBundle_ValidEchoReport_ReturnsRecordWithSourceCitationAndNarrative()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "DiagnosticReport",
                "id": "800",
                "status": "final",
                "code": { "text": "Echocardiogram" },
                "effectiveDateTime": "2026-04-20",
                "conclusion": "LVEF estimated at 35-40%. Moderate mitral regurgitation noted."
              }
            }
          ]
        }
        """;

        var records = DiagnosticReportMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("DiagnosticReport/800");
        record.DocumentType.Should().Be("Echocardiogram");
        record.Status.Should().Be("final");
        record.DateTime.Should().Be(new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero));
        record.NarrativeText.Should().Be("LVEF estimated at 35-40%. Moderate mitral regurgitation noted.");
    }

    [Fact]
    public void MapBundle_MissingConclusion_ReturnsNullNarrativeText()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "DiagnosticReport", "id": "801", "status": "final", "code": { "text": "Lab panel" } } }
          ]
        }
        """;

        var records = DiagnosticReportMapper.MapBundle(json);

        records.Should().ContainSingle().Which.NarrativeText.Should().BeNull();
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
                "resourceType": "DiagnosticReport",
                "id": "802",
                "status": "final",
                "code": { "coding": [ { "display": "Cardiac MRI" } ] }
              }
            }
          ]
        }
        """;

        var records = DiagnosticReportMapper.MapBundle(json);

        records.Should().ContainSingle().Which.DocumentType.Should().Be("Cardiac MRI");
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "DiagnosticReport", "status": "final", "code": { "text": "No id" } } },
            { "resource": { "resourceType": "DiagnosticReport", "id": "803", "status": "final", "code": { "text": "Has id" } } }
          ]
        }
        """;

        var records = DiagnosticReportMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("803");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => DiagnosticReportMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
