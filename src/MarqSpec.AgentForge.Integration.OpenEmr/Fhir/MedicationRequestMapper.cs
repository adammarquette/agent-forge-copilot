using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Maps a FHIR <c>MedicationRequest</c> search-result Bundle to <see cref="MedicationRecord"/>s
/// (INTERFACE_CONTROL.md §B.1). Individually malformed/incomplete entries degrade gracefully
/// (NFR-REL-1) rather than failing the whole bundle; only input malformed enough to not parse as
/// JSON at all throws <see cref="FhirParsingException"/>.
/// </summary>
public static class MedicationRequestMapper
{
    /// <summary>Placeholder used when neither a coded display name nor free text is present.</summary>
    public const string UnknownMedicationDisplay = "Unknown medication";

    private const string ResourceTypeName = "MedicationRequest";

    /// <summary>Maps every <c>MedicationRequest</c> entry in <paramref name="fhirBundleJson"/>.</summary>
    public static IReadOnlyList<MedicationRecord> MapBundle(string fhirBundleJson)
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

        var records = new List<MedicationRecord>();
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
                // Cannot cite a resource with no id - drop rather than fabricate (FR-VERIF-1).
                continue;
            }

            records.Add(new MedicationRecord(
                Source: new ClinicalSourceRef(ResourceTypeName, id),
                MedicationDisplay: ExtractMedicationDisplay(resource),
                Dosage: ExtractDosage(resource),
                Status: (string?)resource["status"] ?? "unknown",
                AuthoredOn: ExtractAuthoredOn(resource)));
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

        return !string.IsNullOrEmpty(display) ? display : UnknownMedicationDisplay;
    }

    private static string? ExtractDosage(JsonNode resource) =>
        resource["dosageInstruction"] is JsonArray { Count: > 0 } instructions
            ? (string?)instructions[0]?["text"]
            : null;

    private static DateTimeOffset? ExtractAuthoredOn(JsonNode resource) =>
        DateTimeOffset.TryParse(
            (string?)resource["authoredOn"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
