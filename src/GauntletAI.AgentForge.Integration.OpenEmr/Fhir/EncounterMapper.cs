using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>Encounter</c> search-result Bundle to <see cref="EncounterRecord"/>s
/// (INTERFACE_CONTROL.md §B.1). Same degradation rules as the other mappers in this namespace.
/// </summary>
public static class EncounterMapper
{
    private const string ResourceTypeName = "Encounter";

    /// <summary>Maps every <c>Encounter</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<EncounterRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<EncounterRecord>();
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

            var period = resource["period"];

            records.Add(new EncounterRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                EncounterType: ExtractEncounterType(resource),
                Status: (string?)resource["status"] ?? "unknown",
                PeriodStart: ParseDateTime((string?)period?["start"]),
                PeriodEnd: ParseDateTime((string?)period?["end"]),
                ReasonDisplay: ExtractReasonDisplay(resource)));
        }

        return records;
    }

    private static string ExtractEncounterType(JsonNode resource) =>
        resource["type"] is JsonArray { Count: > 0 } types && (string?)types[0]?["text"] is { Length: > 0 } text
            ? text
            : "Unknown encounter type";

    private static string? ExtractReasonDisplay(JsonNode resource) =>
        resource["reasonCode"] is JsonArray { Count: > 0 } reasons
            ? (string?)reasons[0]?["text"]
            : null;

    private static DateTimeOffset? ParseDateTime(string? raw) =>
        DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
