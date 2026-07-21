using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>DocumentReference</c> search-result Bundle to <see cref="ClinicalDocumentRecord"/>s
/// (INTERFACE_CONTROL.md §B.1). Narrative text is decoded from an inline base64 attachment when
/// present; a URL-referenced attachment (a separate Binary fetch) is out of scope for this pass
/// and yields a null narrative rather than a second network call. See
/// <see cref="ClinicalDocumentRecord"/> remarks for the scope of "narrative text" here.
/// </summary>
public static class DocumentReferenceMapper
{
    private const string ResourceTypeName = "DocumentReference";

    /// <summary>Maps every <c>DocumentReference</c> entry in <paramref name="fhirBundleJson"/>.</summary>
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
                    (string?)resource["date"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var date)
                    ? date
                    : null,
                NarrativeText: ExtractNarrativeText(resource)));
        }

        return records;
    }

    private static string ExtractDocumentType(JsonNode resource)
    {
        var type = resource["type"];

        var text = (string?)type?["text"];
        if (!string.IsNullOrEmpty(text))
        {
            return text;
        }

        var display = type?["coding"] is JsonArray { Count: > 0 } coding
            ? (string?)coding[0]?["display"]
            : null;

        return !string.IsNullOrEmpty(display) ? display : "Unknown document type";
    }

    private static string? ExtractNarrativeText(JsonNode resource)
    {
        if (resource["content"] is not JsonArray { Count: > 0 } content)
        {
            return null;
        }

        var base64Data = (string?)content[0]?["attachment"]?["data"];
        if (string.IsNullOrEmpty(base64Data))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
        }
        catch (FormatException)
        {
            // Malformed attachment data - degrade to no narrative rather than fail the entry
            // (NFR-REL-1); the document's structured metadata is still surfaced.
            return null;
        }
    }
}
