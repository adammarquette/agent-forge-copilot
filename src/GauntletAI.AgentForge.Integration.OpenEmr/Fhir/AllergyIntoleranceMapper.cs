using System.Text.Json;
using System.Text.Json.Nodes;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>AllergyIntolerance</c> search-result Bundle to <see cref="AllergyRecord"/>s
/// (INTERFACE_CONTROL.md §B.1). Same degradation rules as the other mappers in this namespace.
/// </summary>
public static class AllergyIntoleranceMapper
{
    private const string ResourceTypeName = "AllergyIntolerance";

    /// <summary>Maps every <c>AllergyIntolerance</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<AllergyRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<AllergyRecord>();
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

            records.Add(new AllergyRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                AllergenDisplay: ExtractAllergenDisplay(resource),
                ClinicalStatus: ExtractClinicalStatus(resource),
                Criticality: (string?)resource["criticality"],
                ReactionManifestation: ExtractReactionManifestation(resource)));
        }

        return records;
    }

    private static string ExtractAllergenDisplay(JsonNode resource)
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

        return !string.IsNullOrEmpty(display) ? display : "Unknown allergen";
    }

    private static string ExtractClinicalStatus(JsonNode resource)
    {
        var coding = resource["clinicalStatus"]?["coding"] as JsonArray;
        var code = coding is { Count: > 0 } ? (string?)coding[0]?["code"] : null;
        return code ?? "unknown";
    }

    private static string? ExtractReactionManifestation(JsonNode resource)
    {
        if (resource["reaction"] is not JsonArray { Count: > 0 } reactions)
        {
            return null;
        }

        var manifestations = reactions[0]?["manifestation"] as JsonArray;
        return manifestations is { Count: > 0 } ? (string?)manifestations[0]?["text"] : null;
    }
}
