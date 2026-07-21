using System.Text.Json;
using MarqSpec.AgentForge.Data.Entities;

namespace MarqSpec.AgentForge.Agents;

/// <summary>
/// Parses the structured per-fact citations (page + bounding box + quote) out of the validated extraction
/// JSON for the click-to-source overlay (FR-CITE-2, gitlab#96). The answer path already validated the schema
/// against the strict contract, so this is a read-only projection: anything unparseable degrades to "no
/// citations", never a throw. The bounding box is what the answer-path projections (<c>LabFact</c>,
/// <c>DerivedFactView</c>) drop, so it is recovered here rather than re-plumbed through them.
/// </summary>
public static class DocumentCitationExtractor
{
    /// <summary>Projects the citable regions out of one extraction's canonical JSON, by document type.</summary>
    public static IReadOnlyList<DocumentCitation> Extract(string? factsJson, ClinicalDocumentType documentType)
    {
        if (string.IsNullOrWhiteSpace(factsJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(factsJson);
            return documentType switch
            {
                ClinicalDocumentType.LabPdf => ExtractLab(doc.RootElement),
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<DocumentCitation> ExtractLab(JsonElement root)
    {
        var citations = new List<DocumentCitation>();
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("tests", out var tests)
            || tests.ValueKind != JsonValueKind.Array)
        {
            return citations;
        }

        foreach (var test in tests.EnumerateArray())
        {
            // The citation is the grounding gate: a fact without one is not click-to-source-able, so skip it.
            if (test.ValueKind != JsonValueKind.Object
                || !test.TryGetProperty("citation", out var citation)
                || citation.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = ReadString(test, "test_name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var slug = Slugify(name);
            if (slug.Length == 0)
            {
                continue;
            }

            citations.Add(new DocumentCitation(
                slug,
                name,
                ReadString(test, "value") ?? "?",
                ReadInt(citation, "page") ?? 1,
                ReadBoundingBox(citation),
                ReadString(citation, "quote")));
        }

        return citations;
    }

    // A malformed or non-4-element bbox degrades to page-level (null) rather than a partial region.
    private static double[]? ReadBoundingBox(JsonElement citation)
    {
        if (!citation.TryGetProperty("bounding_box", out var bbox) || bbox.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<double>(4);
        foreach (var component in bbox.EnumerateArray())
        {
            if (component.ValueKind != JsonValueKind.Number || !component.TryGetDouble(out var d))
            {
                return null;
            }

            values.Add(d);
        }

        return values.Count == 4 ? [.. values] : null;
    }

    // reference: mirrors EvidenceAgentSupervisor.Slugify — the [Lab/<slug>] id here must match the answer token
    // so a clicked citation resolves to this region.
    private static string Slugify(string value) =>
        new(value.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.').ToArray());

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var n)
            ? n
            : null;
}
