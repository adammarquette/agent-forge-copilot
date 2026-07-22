using System.Text.Json.Serialization;

namespace MarqSpec.AgentForge.Agents;

/// <summary>
/// Citation-shaped projection of an evidence snippet handed to the critic. The Week 1 source-attribution
/// scanner recognizes a citation from any object carrying <c>ResourceType</c> + <c>Id</c> string fields, so
/// evidence is projected to that shape (ResourceType "Guideline", Id the chunk id) — letting the composer
/// cite <c>[Guideline/&lt;chunkId&gt;]</c> and have the attribution engine resolve it (W2_ARCHITECTURE.md §6/§7).
/// </summary>
internal sealed record EvidenceToolResult(string ResourceType, string Id, string Section, string Text);

/// <summary>
/// Citation-shaped projection of an extracted patient lab value handed to the critic. Same mechanism as
/// <see cref="EvidenceToolResult"/> (ResourceType "Lab", Id a whitespace-free slug of the test name), so the
/// composer can cite <c>[Lab/&lt;slug&gt;]</c> for a patient-specific value and have it resolve rather than be
/// suppressed as an uncited clinical claim.
/// </summary>
internal sealed record LabToolResult(string ResourceType, string Id, string TestName, string Value);

/// <summary>
/// Citation-shaped projection of a fact ingested from a document <b>before</b> this turn (the pre-visit E2
/// path). Same mechanism as <see cref="LabToolResult"/> (ResourceType "Derived", Id a per-fact slug), so the
/// composer can cite <c>[Derived/&lt;slug&gt;]</c> for a document-derived value and have it resolve — the
/// <c>source_type: derived</c> class the answer keeps distinct from <c>fhir</c>/<c>guideline</c> (FR-RAG-3).
/// </summary>
internal sealed record DerivedToolResult(string ResourceType, string Id, string FactType, string Value);

/// <summary>
/// Source-generated JSON for handing evidence and extracted labs to the critic as tool results
/// (ENGINEERING_STANDARDS.md §3). Default (PascalCase) naming so the <c>ResourceType</c>/<c>Id</c> keys match
/// the scanner exactly.
/// </summary>
[JsonSerializable(typeof(EvidenceToolResult[]))]
[JsonSerializable(typeof(LabToolResult[]))]
[JsonSerializable(typeof(DerivedToolResult[]))]
internal sealed partial class AgentsJsonContext : JsonSerializerContext;
