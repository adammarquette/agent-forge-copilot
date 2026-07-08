using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>DiagnosticReport</c> search-result Bundle to <see cref="ClinicalDocumentRecord"/>s
/// (INTERFACE_CONTROL.md §B.1). See <see cref="ClinicalDocumentRecord"/> remarks for the scope of
/// what "narrative text" means here versus clinical-value extraction.
/// </summary>
public static class DiagnosticReportMapper
{
    private const string ResourceTypeName = "DiagnosticReport";

    /// <summary>Maps every <c>DiagnosticReport</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<ClinicalDocumentRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<ClinicalDocumentRecord>();
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

            records.Add(new ClinicalDocumentRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                DocumentType: ExtractDocumentType(resource),
                Status: (string?)resource["status"],
                DateTime: DateTimeOffset.TryParse(
                    (string?)resource["effectiveDateTime"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var effective)
                    ? effective
                    : null,
                NarrativeText: (string?)resource["conclusion"]));
        }

        return records;
    }

    private static string ExtractDocumentType(JsonNode resource)
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

        return !string.IsNullOrEmpty(display) ? display : "Unknown report type";
    }
}
