using AgentForge.Integration.OpenEmr.Fhir;

namespace AgentForge.Mcp;

/// <summary>Result of the <c>get_documents</c> tool.</summary>
/// <param name="Documents">DiagnosticReport and DocumentReference records, combined.</param>
public sealed record DocumentsResult(IReadOnlyList<ClinicalDocumentRecord> Documents);
