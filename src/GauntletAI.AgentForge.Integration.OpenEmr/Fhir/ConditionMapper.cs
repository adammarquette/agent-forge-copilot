using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>Condition</c> search-result Bundle to <see cref="ConditionRecord"/>s
/// (INTERFACE_CONTROL.md §B.1). Follows the same degradation rules as
/// <see cref="MedicationRequestMapper"/>: per-entry, never fabricated, never a whole-bundle
/// failure except for input that isn't valid JSON at all.
/// </summary>
public static class ConditionMapper
{
    private const string ResourceTypeName = "Condition";

    /// <summary>Maps every <c>Condition</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<ConditionRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<ConditionRecord>();
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

            records.Add(new ConditionRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                ProblemDisplay: ExtractProblemDisplay(resource),
                ClinicalStatus: ExtractClinicalStatus(resource),
                OnsetDate: ExtractOnsetDate(resource)));
        }

        return records;
    }

    private static string ExtractProblemDisplay(JsonNode resource)
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

        return !string.IsNullOrEmpty(display) ? display : "Unknown problem";
    }

    private static string ExtractClinicalStatus(JsonNode resource)
    {
        var coding = resource["clinicalStatus"]?["coding"] as JsonArray;
        var code = coding is { Count: > 0 } ? (string?)coding[0]?["code"] : null;
        return code ?? "unknown";
    }

    private static DateTimeOffset? ExtractOnsetDate(JsonNode resource) =>
        DateTimeOffset.TryParse(
            (string?)resource["onsetDateTime"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
