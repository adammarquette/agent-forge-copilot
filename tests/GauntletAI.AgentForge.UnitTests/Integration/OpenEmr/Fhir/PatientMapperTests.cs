using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class PatientMapperTests
{
    [Fact]
    public void MapResource_ValidPatient_ReturnsRecordWithSourceCitation()
    {
        const string json = """
        {
          "resourceType": "Patient",
          "id": "1",
          "name": [ { "family": "Smith", "given": [ "Jane" ] } ],
          "birthDate": "1955-03-12",
          "gender": "female"
        }
        """;

        var record = PatientMapper.MapResource(json);

        record.Should().NotBeNull();
        record!.Source.Citation.Should().Be("Patient/1");
        record.DisplayName.Should().Be("Jane Smith");
        record.BirthDate.Should().Be(new DateOnly(1955, 3, 12));
        record.Gender.Should().Be("female");
    }

    [Fact]
    public void MapResource_MultipleGivenNames_JoinsThemInOrder()
    {
        const string json = """
        {
          "resourceType": "Patient",
          "id": "2",
          "name": [ { "family": "Doe", "given": [ "Mary", "Ann" ] } ]
        }
        """;

        var record = PatientMapper.MapResource(json);

        record!.DisplayName.Should().Be("Mary Ann Doe");
    }

    [Fact]
    public void MapResource_MissingName_ReturnsUnknownPatientPlaceholder()
    {
        const string json = """{ "resourceType": "Patient", "id": "3" }""";

        var record = PatientMapper.MapResource(json);

        record!.DisplayName.Should().Be(PatientMapper.UnknownPatientDisplay);
    }

    [Fact]
    public void MapResource_MissingBirthDateAndGender_ReturnsNullForBoth()
    {
        const string json = """{ "resourceType": "Patient", "id": "4", "name": [ { "family": "X" } ] }""";

        var record = PatientMapper.MapResource(json);

        record!.BirthDate.Should().BeNull();
        record.Gender.Should().BeNull();
    }

    [Fact]
    public void MapResource_MissingId_ReturnsNullRatherThanFabricatingACitation()
    {
        const string json = """{ "resourceType": "Patient", "name": [ { "family": "X" } ] }""";

        var record = PatientMapper.MapResource(json);

        record.Should().BeNull();
    }

    [Fact]
    public void MapResource_NotAPatientResource_ReturnsNull()
    {
        const string json = """{ "resourceType": "Condition", "id": "5" }""";

        var record = PatientMapper.MapResource(json);

        record.Should().BeNull();
    }

    [Fact]
    public void MapResource_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => PatientMapper.MapResource("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }
}
