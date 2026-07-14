using System.Text.Json.Serialization;

namespace GauntletAI.AgentForge.Documents.Extraction;

/// <summary>
/// Source-generated JSON context for the extraction contracts (ENGINEERING_STANDARDS.md §3). Uses
/// snake_case property names — the wire shape the extraction prompt instructs the model to return — and
/// enforces required members on deserialization, which is the schema-is-the-gate mechanism (NFR-CONTRACT-1).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LabExtraction))]
[JsonSerializable(typeof(IntakeExtraction))]
public partial class DocumentExtractionJsonContext : JsonSerializerContext;
