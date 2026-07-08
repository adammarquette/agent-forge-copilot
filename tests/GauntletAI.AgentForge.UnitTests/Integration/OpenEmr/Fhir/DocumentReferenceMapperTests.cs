using System.Text;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class DocumentReferenceMapperTests
{
    [Fact]
    public void MapBundle_ValidDeviceInterrogationDocument_ReturnsRecordWithDecodedNarrative()
    {
        var narrative = "ICD interrogation: battery 4.2V, no shocks delivered since last visit.";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(narrative));
        var json = $$"""
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "DocumentReference",
                "id": "900",
                "status": "current",
                "type": { "text": "Device Interrogation Report" },
                "date": "2026-04-22T09:00:00Z",
                "content": [ { "attachment": { "contentType": "text/plain", "data": "{{base64}}" } } ]
              }
            }
          ]
        }
        """;

        var records = DocumentReferenceMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("DocumentReference/900");
        record.DocumentType.Should().Be("Device Interrogation Report");
        record.Status.Should().Be("current");
        record.DateTime.Should().Be(new DateTimeOffset(2026, 4, 22, 9, 0, 0, TimeSpan.Zero));
        record.NarrativeText.Should().Be(narrative);
    }

    [Fact]
    public void MapBundle_ContentIsUrlReferenceNotInlineData_ReturnsNullNarrativeText()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "DocumentReference",
                "id": "901",
                "status": "current",
                "type": { "text": "Echo Report" },
                "content": [ { "attachment": { "url": "Binary/xyz" } } ]
              }
            }
          ]
        }
        """;

        var records = DocumentReferenceMapper.MapBundle(json);

        records.Should().ContainSingle().Which.NarrativeText.Should().BeNull();
    }

    [Fact]
    public void MapBundle_MissingContent_ReturnsNullNarrativeText()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "DocumentReference", "id": "902", "status": "current", "type": { "text": "X" } } }
          ]
        }
        """;

        var records = DocumentReferenceMapper.MapBundle(json);

        records.Should().ContainSingle().Which.NarrativeText.Should().BeNull();
    }

    [Fact]
    public void MapBundle_MalformedBase64Data_ReturnsNullNarrativeTextRatherThanThrowing()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "DocumentReference",
                "id": "903",
                "status": "current",
                "type": { "text": "X" },
                "content": [ { "attachment": { "data": "not-valid-base64!!!" } } ]
              }
            }
          ]
        }
        """;

        var records = DocumentReferenceMapper.MapBundle(json);

        records.Should().ContainSingle().Which.NarrativeText.Should().BeNull();
    }

    [Fact]
    public void MapBundle_TypeTextMissing_FallsBackToCodingDisplay()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "DocumentReference",
                "id": "904",
                "status": "current",
                "type": { "coding": [ { "display": "Outside Records" } ] }
              }
            }
          ]
        }
        """;

        var records = DocumentReferenceMapper.MapBundle(json);

        records.Should().ContainSingle().Which.DocumentType.Should().Be("Outside Records");
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "DocumentReference", "status": "current", "type": { "text": "No id" } } },
            { "resource": { "resourceType": "DocumentReference", "id": "905", "status": "current", "type": { "text": "Has id" } } }
          ]
        }
        """;

        var records = DocumentReferenceMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("905");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => DocumentReferenceMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
