using System.Text.Json.Serialization;

namespace GauntletAI.AgentForge.Agents;

/// <summary>Source-generated JSON for handing evidence to the critic as tool results (ENGINEERING_STANDARDS.md §3).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EvidenceSnippet[]))]
internal sealed partial class AgentsJsonContext : JsonSerializerContext;
