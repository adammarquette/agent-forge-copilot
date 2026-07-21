using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class AppointmentMapperTests
{
    [Fact]
    public void MapBundle_ValidAppointmentWithNpiProvider_ReturnsRecordWithPatientAndPractitionerReference()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "Appointment",
                "id": "900",
                "status": "booked",
                "start": "2026-07-11T14:00:00Z",
                "end": "2026-07-11T14:30:00Z",
                "participant": [
                  { "actor": { "reference": "Patient/patient-uuid-1" } },
                  { "actor": { "reference": "Practitioner/provider-uuid-1" } },
                  { "actor": { "reference": "Location/facility-uuid-1" } }
                ]
              }
            }
          ]
        }
        """;

        var records = AppointmentMapper.MapBundle(json);

        records.Should().ContainSingle();
        var record = records[0];
        record.Source.Citation.Should().Be("Appointment/900");
        record.Status.Should().Be("booked");
        record.ScheduledStart.Should().Be(new DateTimeOffset(2026, 7, 11, 14, 0, 0, TimeSpan.Zero));
        record.PatientId.Should().Be("patient-uuid-1");
        record.ProviderActorReference.Should().Be("Practitioner/provider-uuid-1");
    }

    [Fact]
    public void MapBundle_ProviderWithoutNpi_ReturnsPersonReferenceNotPractitioner()
    {
        // FhirAppointmentService only emits a Practitioner/ actor when the provider has an NPI on
        // file - otherwise the same provider is referenced as Person/. The roster filter (Phase 3)
        // must handle both, or a provider without an NPI would silently vanish from their own
        // agenda (ARCHITECTURE.md §19.2a).
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            {
              "resource": {
                "resourceType": "Appointment",
                "id": "901",
                "status": "booked",
                "participant": [
                  { "actor": { "reference": "Patient/patient-uuid-2" } },
                  { "actor": { "reference": "Person/provider-uuid-2" } }
                ]
              }
            }
          ]
        }
        """;

        var records = AppointmentMapper.MapBundle(json);

        records.Should().ContainSingle().Which.ProviderActorReference.Should().Be("Person/provider-uuid-2");
    }

    [Fact]
    public void MapBundle_NoPatientOrProviderParticipant_ReturnsNullForBothRatherThanThrowing()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Appointment", "id": "902", "status": "booked" } }
          ]
        }
        """;

        var records = AppointmentMapper.MapBundle(json);

        var record = records.Should().ContainSingle().Subject;
        record.PatientId.Should().BeNull();
        record.ProviderActorReference.Should().BeNull();
    }

    [Fact]
    public void MapBundle_MissingStart_ReturnsNullScheduledStart()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Appointment", "id": "903", "status": "pending" } }
          ]
        }
        """;

        var records = AppointmentMapper.MapBundle(json);

        records.Should().ContainSingle().Which.ScheduledStart.Should().BeNull();
    }

    [Fact]
    public void MapBundle_EntryMissingId_SkipsEntryRatherThanFabricatingACitation()
    {
        const string json = """
        {
          "resourceType": "Bundle",
          "entry": [
            { "resource": { "resourceType": "Appointment", "status": "booked" } },
            { "resource": { "resourceType": "Appointment", "id": "904", "status": "booked" } }
          ]
        }
        """;

        var records = AppointmentMapper.MapBundle(json);

        records.Should().ContainSingle().Which.Source.Id.Should().Be("904");
    }

    [Fact]
    public void MapBundle_MalformedJson_ThrowsFhirParsingException()
    {
        var act = () => AppointmentMapper.MapBundle("{ not valid");

        act.Should().Throw<FhirParsingException>();
    }

    [Theory]
    [InlineData("Practitioner/provider-uuid-1", "provider-uuid-1", true)]
    [InlineData("Person/provider-uuid-1", "provider-uuid-1", true)]
    [InlineData("Practitioner/provider-uuid-1", "someone-else", false)]
    [InlineData(null, "provider-uuid-1", false)]
    public void IsForProvider_MatchesEitherPractitionerOrPersonReferenceForTheGivenClinicianIdentity(
        string? providerActorReference, string clinicianIdentity, bool expected)
    {
        var record = new AppointmentRecord(
            new ClinicalSourceRef("Appointment", "900"),
            PatientId: "patient-uuid-1",
            ProviderActorReference: providerActorReference,
            Status: "booked",
            ScheduledStart: null);

        record.IsForProvider(clinicianIdentity).Should().Be(expected);
    }
}
