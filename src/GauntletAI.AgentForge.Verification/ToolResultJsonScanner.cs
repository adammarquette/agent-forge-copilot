using System.Text.Json;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// Walks the raw JSON of every tool result returned this turn to recover (a) the set of
/// resolvable citations and (b) the structured clinical data the domain-constraint rules need -
/// both derived from the same source of truth (what tools actually returned), never from the
/// model's prose. Generic by design: it recognizes record shapes structurally (which fields are
/// present), so it works for any tool's result without a tool-name-to-type lookup table to keep
/// in sync.
/// </summary>
public static class ToolResultJsonScanner
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Scans every blob in <paramref name="toolResultJson"/>. Malformed or error-shaped blobs are skipped, not thrown on.</summary>
    public static ToolResultScan Scan(IReadOnlyCollection<string> toolResultJson)
    {
        var citations = new HashSet<string>(StringComparer.Ordinal);
        List<MedicationRecord> medications = [];
        List<ObservationRecord> labs = [];
        List<ConditionRecord> problems = [];

        foreach (var json in toolResultJson)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                Walk(document.RootElement, citations, medications, labs, problems);
            }
        }

        return new ToolResultScan(citations, new DomainConstraintInput(medications, labs, problems));
    }

    private static void Walk(
        JsonElement element,
        HashSet<string> citations,
        List<MedicationRecord> medications,
        List<ObservationRecord> labs,
        List<ConditionRecord> problems)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (TryGetString(element, "ResourceType", out var resourceType) && TryGetString(element, "Id", out var id))
                {
                    citations.Add($"{resourceType}/{id}");
                }

                if (HasAll(element, "MedicationDisplay", "Status", "Source"))
                {
                    AddIfNotNull(medications, element.Deserialize<MedicationRecord>(DeserializeOptions));
                }
                else if (HasAll(element, "CodeDisplay", "Category", "Source"))
                {
                    AddIfNotNull(labs, element.Deserialize<ObservationRecord>(DeserializeOptions));
                }
                else if (HasAll(element, "ProblemDisplay", "ClinicalStatus", "Source"))
                {
                    AddIfNotNull(problems, element.Deserialize<ConditionRecord>(DeserializeOptions));
                }

                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, citations, medications, labs, problems);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, citations, medications, labs, problems);
                }

                break;
        }
    }

    private static void AddIfNotNull<T>(List<T> list, T? value)
    {
        if (value is not null)
        {
            list.Add(value);
        }
    }

    private static bool HasAll(JsonElement element, params string[] propertyNames) =>
        propertyNames.All(name => element.TryGetProperty(name, out _));

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
