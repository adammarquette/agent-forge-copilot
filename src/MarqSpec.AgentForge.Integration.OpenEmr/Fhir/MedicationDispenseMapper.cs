using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>MedicationDispense</c> search-result Bundle to
/// <see cref="MedicationDispenseRecord"/>s (INTERFACE_CONTROL.md §B.1). Same degradation rules
/// as the other mappers in this namespace.
/// </summary>
public static class MedicationDispenseMapper
{
    private const string ResourceTypeName = "MedicationDispense";

    /// <summary>Maps every <c>MedicationDispense</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<MedicationDispenseRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<MedicationDispenseRecord>();
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

            records.Add(new MedicationDispenseRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                MedicationDisplay: ExtractMedicationDisplay(resource),
                Status: (string?)resource["status"] ?? "unknown",
                WhenHandedOver: DateTimeOffset.TryParse(
                    (string?)resource["whenHandedOver"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var handedOver)
                    ? handedOver
                    : null,
                DaysSupply: (double?)resource["daysSupply"]?["value"]));
        }

        return records;
    }

    private static string ExtractMedicationDisplay(JsonNode resource)
    {
        var concept = resource["medicationCodeableConcept"];

        var text = (string?)concept?["text"];
        if (!string.IsNullOrEmpty(text))
        {
            return text;
        }

        var display = concept?["coding"] is JsonArray { Count: > 0 } coding
            ? (string?)coding[0]?["display"]
            : null;

        return !string.IsNullOrEmpty(display) ? display : MedicationRequestMapper.UnknownMedicationDisplay;
    }
}
