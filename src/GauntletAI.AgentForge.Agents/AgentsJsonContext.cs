using System.Text.Json.Serialization;

namespace GauntletAI.AgentForge.Agents;

/// <summary>
/// Citation-shaped projection of an evidence snippet handed to the critic. The Week 1 source-attribution
/// scanner recognizes a citation from any object carrying <c>ResourceType</c> + <c>Id</c> string fields, so
/// evidence is projected to that shape (ResourceType "Guideline", Id the chunk id) — letting the composer
/// cite <c>[Guideline/&lt;chunkId&gt;]</c> and have the attribution engine resolve it (W2_ARCHITECTURE.md §6/§7).
/// </summary>
internal sealed record EvidenceToolResult(string ResourceType, string Id, string Section, string Text);

/// <summary>
/// Source-generated JSON for handing evidence to the critic as tool results (ENGINEERING_STANDARDS.md §3).
/// Default (PascalCase) naming so the <c>ResourceType</c>/<c>Id</c> keys match the scanner exactly.
/// </summary>
[JsonSerializable(typeof(EvidenceToolResult[]))]
internal sealed partial class AgentsJsonContext : JsonSerializerContext;
