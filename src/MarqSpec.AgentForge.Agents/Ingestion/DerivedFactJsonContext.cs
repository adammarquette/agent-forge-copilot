using System.Text.Json.Serialization;
using MarqSpec.AgentForge.Documents.Extraction;

namespace MarqSpec.AgentForge.Agents.Ingestion;

/// <summary>
/// Source-generated JSON for reading a canonical extraction and re-serializing individual facts as their
/// <c>PayloadJson</c>. Snake_case to match the extraction wire shape (DocumentExtractionJsonContext).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LabExtraction))]
[JsonSerializable(typeof(IntakeExtraction))]
[JsonSerializable(typeof(LabTestResult))]
[JsonSerializable(typeof(IntakeMedication))]
[JsonSerializable(typeof(IntakeDemographics))]
[JsonSerializable(typeof(TextFactPayload))]
internal sealed partial class DerivedFactJsonContext : JsonSerializerContext;

/// <summary>Payload for a single free-text intake fact (chief concern, allergy, family-history item).</summary>
internal sealed record TextFactPayload(string Value);
