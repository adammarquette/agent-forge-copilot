using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>Appointment</c> search-result Bundle to <see cref="AppointmentRecord"/>s
/// (INTERFACE_CONTROL.md §B.1, ARCHITECTURE.md §19). Same degradation rules as the other mappers
/// in this namespace - a missing participant is a gap, not a parse failure.
/// </summary>
public static class AppointmentMapper
{
    private const string ResourceTypeName = "Appointment";

    /// <summary>Maps every <c>Appointment</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<AppointmentRecord> MapBundle(string fhirBundleJson)
    {
        JsonNode? bundle;
        try
        {
            bundle = JsonNode.Parse(fhirBundleJson);
        }
        catch (JsonException ex)
        {
            throw new FhirParsingException($"{ResourceTypeName} bundle is not valid JSON.", ex);
        }

        if (bundle?["entry"] is not JsonArray entries)
        {
            return [];
        }

        var records = new List<AppointmentRecord>();
        foreach (var entry in entries)
        {
            var resource = entry?["resource"];
            if (resource is null || (string?)resource["resourceType"] != ResourceTypeName)
            {
                continue;
            }

            var id = (string?)resource["id"];
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var (patientId, providerActorReference) = ExtractParticipants(resource);

            records.Add(new AppointmentRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                PatientId: patientId,
                ProviderActorReference: providerActorReference,
                Status: (string?)resource["status"] ?? "unknown",
                ScheduledStart: ParseDateTime((string?)resource["start"])));
        }

        return records;
    }

    /// <summary>
    /// Reads each participant's <c>actor.reference</c> (e.g. <c>Patient/{uuid}</c>,
    /// <c>Practitioner/{uuid}</c>, <c>Person/{uuid}</c>, <c>Location/{uuid}</c>) and routes it by
    /// the reference's own resource-type prefix - simpler and just as reliable as reading the
    /// participant's coded type, since each actor kind maps 1:1 to one of those FHIR resource types.
    /// </summary>
    private static (string? PatientId, string? ProviderActorReference) ExtractParticipants(JsonNode resource)
    {
        if (resource["participant"] is not JsonArray participants)
        {
            return (null, null);
        }

        string? patientId = null;
        string? providerActorReference = null;
        foreach (var participant in participants)
        {
            var reference = (string?)participant?["actor"]?["reference"];
            if (string.IsNullOrEmpty(reference))
            {
                continue;
            }

            if (reference.StartsWith("Patient/", StringComparison.Ordinal))
            {
                patientId = reference["Patient/".Length..];
            }
            else if (reference.StartsWith("Practitioner/", StringComparison.Ordinal) ||
                     reference.StartsWith("Person/", StringComparison.Ordinal))
            {
                providerActorReference = reference;
            }
        }

        return (patientId, providerActorReference);
    }

    private static DateTimeOffset? ParseDateTime(string? raw) =>
        DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
