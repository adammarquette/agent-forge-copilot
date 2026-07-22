using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>Observation</c> search-result Bundle (laboratory or vital-signs category) to
/// <see cref="ObservationRecord"/>s (INTERFACE_CONTROL.md §B.1). Same degradation rules as the
/// other mappers in this namespace.
/// </summary>
public static class ObservationMapper
{
    private const string ResourceTypeName = "Observation";

    /// <summary>Maps every <c>Observation</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<ObservationRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<ObservationRecord>();
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

            var (low, high) = ExtractReferenceRange(resource);

            records.Add(new ObservationRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                Category: ExtractCategory(resource),
                CodeDisplay: ExtractCodeDisplay(resource),
                Value: (double?)resource["valueQuantity"]?["value"],
                Unit: (string?)resource["valueQuantity"]?["unit"],
                ReferenceRangeLow: low,
                ReferenceRangeHigh: high,
                EffectiveDateTime: ExtractEffectiveDateTime(resource),
                Status: (string?)resource["status"] ?? "unknown"));
        }

        return records;
    }

    private static string ExtractCategory(JsonNode resource)
    {
        var categories = resource["category"] as JsonArray;
        var coding = categories is { Count: > 0 } ? categories[0]?["coding"] as JsonArray : null;
        var code = coding is { Count: > 0 } ? (string?)coding[0]?["code"] : null;
        return code ?? "unknown";
    }

    private static string ExtractCodeDisplay(JsonNode resource)
    {
        var code = resource["code"];

        var text = (string?)code?["text"];
        if (!string.IsNullOrEmpty(text))
        {
            return text;
        }

        var display = code?["coding"] is JsonArray { Count: > 0 } coding
            ? (string?)coding[0]?["display"]
            : null;

        return !string.IsNullOrEmpty(display) ? display : "Unknown observation";
    }

    private static (double? Low, double? High) ExtractReferenceRange(JsonNode resource)
    {
        if (resource["referenceRange"] is not JsonArray { Count: > 0 } ranges)
        {
            return (null, null);
        }

        var range = ranges[0];
        return ((double?)range?["low"]?["value"], (double?)range?["high"]?["value"]);
    }

    private static DateTimeOffset? ExtractEffectiveDateTime(JsonNode resource) =>
        DateTimeOffset.TryParse(
            (string?)resource["effectiveDateTime"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
