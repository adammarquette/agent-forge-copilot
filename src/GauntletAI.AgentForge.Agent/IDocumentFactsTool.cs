namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// Reads the patient's sidecar-extracted document facts as citable records for the <c>get_document_facts</c>
/// tool (gitlab#116). Behind an interface so the dispatcher stays unit-testable and so the tool degrades
/// cleanly (empty) when no document store is wired. The access audit lives in the implementation — this is
/// sidecar-native PHI that does not pass through the MCP server's <c>AuditingMcpToolServer</c> choke point.
/// </summary>
public interface IDocumentFactsTool
{
    /// <summary>The patient's document-derived facts as citable <c>[Document/&lt;id&gt;]</c> records; empty when none on file.</summary>
    Task<DocumentFactsResult> GetAsync(string patientId, CancellationToken cancellationToken);
}

/// <summary>Result of <c>get_document_facts</c>: the patient's document-derived facts as citable records.</summary>
public sealed record DocumentFactsResult(IReadOnlyList<DocumentFactRecord> Facts);

/// <summary>
/// One document-derived fact the model may cite. <see cref="ResourceType"/> is always <c>"Document"</c> and
/// <see cref="Id"/> is the citation slug, so a <c>[Document/{Id}]</c> citation resolves against the verifier's
/// attribution scanner (the same <c>[ResourceType/Id]</c> shape the FHIR tools emit) instead of being
/// suppressed as uncited.
/// </summary>
/// <param name="ResourceType">Always <c>"Document"</c>.</param>
/// <param name="Id">Citation slug — the anchor for a <c>[Document/{Id}]</c> citation.</param>
/// <param name="FactType">Fact category, e.g. <c>lab.result</c>.</param>
/// <param name="Value">The asserted value or quoted snippet the fact carries.</param>
/// <param name="SourceDocumentId">OpenEMR DocumentReference id the client fetches to open the source PDF.</param>
/// <param name="Page">Source page/section, when known.</param>
public sealed record DocumentFactRecord(
    string ResourceType, string Id, string FactType, string Value, string SourceDocumentId, string? Page);
