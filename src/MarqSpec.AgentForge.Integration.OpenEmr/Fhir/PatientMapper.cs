using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a single FHIR <c>Patient</c> resource (not a Bundle - Patient is fetched by direct read,
/// INTERFACE_CONTROL.md §B.2) to a <see cref="PatientRecord"/>.
/// </summary>
public static class PatientMapper
{
    /// <summary>Placeholder used when no name is present at all.</summary>
    public const string UnknownPatientDisplay = "Unknown patient";

    private const string ResourceTypeName = "Patient";

    /// <summary>
    /// Maps <paramref name="fhirResourceJson"/>, or returns <see langword="null"/> if it isn't a
    /// <c>Patient</c> resource or has no id (a resource with no id can't be cited, FR-VERIF-1).
    /// </summary>
    public static PatientRecord? MapResource(string fhirResourceJson)
    {
        JsonNode? resource;
        try
        {
            resource = JsonNode.Parse(fhirResourceJson);
        }
        catch (JsonException ex)
        {
            throw new FhirParsingException($"{ResourceTypeName} resource is not valid JSON.", ex);
        }

        if (resource is null || (string?)resource["resourceType"] != ResourceTypeName)
        {
            return null;
        }

        var id = (string?)resource["id"];
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return new PatientRecord(
            Source: new ClinicalSourceRef(ResourceTypeName, id),
            DisplayName: ExtractDisplayName(resource),
            BirthDate: ExtractBirthDate(resource),
            Gender: (string?)resource["gender"]);
    }

    private static string ExtractDisplayName(JsonNode resource)
    {
        if (resource["name"] is not JsonArray { Count: > 0 } names)
        {
            return UnknownPatientDisplay;
        }

        var name = names[0];
        var given = name?["given"] is JsonArray givenNames
            ? string.Join(' ', givenNames.Select(n => (string?)n).Where(n => !string.IsNullOrEmpty(n)))
            : string.Empty;
        var family = (string?)name?["family"] ?? string.Empty;

        var fullName = string.Join(' ', new[] { given, family }.Where(part => part.Length > 0));
        return fullName.Length > 0 ? fullName : UnknownPatientDisplay;
    }

    private static DateOnly? ExtractBirthDate(JsonNode resource) =>
        DateOnly.TryParseExact(
            (string?)resource["birthDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
