using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>Procedure</c> search-result Bundle to <see cref="ProcedureRecord"/>s
/// (INTERFACE_CONTROL.md §B.1). Same degradation rules as the other mappers in this namespace.
/// </summary>
public static class ProcedureMapper
{
    private const string ResourceTypeName = "Procedure";

    /// <summary>Maps every <c>Procedure</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<ProcedureRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<ProcedureRecord>();
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

            records.Add(new ProcedureRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                ProcedureDisplay: ExtractProcedureDisplay(resource),
                Status: (string?)resource["status"] ?? "unknown",
                PerformedDate: DateTimeOffset.TryParse(
                    (string?)resource["performedDateTime"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed)
                    ? parsed
                    : null));
        }

        return records;
    }

    private static string ExtractProcedureDisplay(JsonNode resource)
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

        return !string.IsNullOrEmpty(display) ? display : "Unknown procedure";
    }
}
