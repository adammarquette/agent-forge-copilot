using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.Mcp;

/// <summary>Result of the <c>get_documents</c> tool.</summary>
/// <param name="Documents">DiagnosticReport and DocumentReference records, combined.</param>
public sealed record DocumentsResult(IReadOnlyList<ClinicalDocumentRecord> Documents);
