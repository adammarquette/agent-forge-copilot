namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// Retrieves grounded guideline snippets for the <c>retrieve_evidence</c> tool (gitlab#117): the multi-turn
/// chat can ground and cite an answer in the clinical-guideline corpus (not just OpenEMR data) as
/// <c>[Guideline/&lt;chunkId&gt;]</c>. Behind an interface so the dispatcher stays unit-testable and degrades
/// cleanly (empty) when no retriever is wired. Guideline evidence is not patient data, so no access audit is
/// required.
/// </summary>
public interface IEvidenceTool
{
    /// <summary>The top guideline snippets for <paramref name="query"/> as citable <c>[Guideline/&lt;id&gt;]</c> records; empty when none.</summary>
    Task<EvidenceResult> GetAsync(string query, CancellationToken cancellationToken);
}

/// <summary>Result of <c>retrieve_evidence</c>: grounded guideline snippets as citable records.</summary>
public sealed record EvidenceResult(IReadOnlyList<EvidenceRecord> Snippets);

/// <summary>
/// One guideline snippet the model may cite. <see cref="ResourceType"/> is always <c>"Guideline"</c> and
/// <see cref="Id"/> is the chunk id, so a <c>[Guideline/{Id}]</c> citation resolves against the verifier's
/// attribution scanner (the same <c>[ResourceType/Id]</c> shape the other tools emit).
/// </summary>
/// <param name="ResourceType">Always <c>"Guideline"</c>.</param>
/// <param name="Id">The chunk id — the anchor for a <c>[Guideline/{Id}]</c> citation.</param>
/// <param name="DocumentId">The guideline document the snippet came from.</param>
/// <param name="Section">Section heading within the document.</param>
/// <param name="Text">The snippet text.</param>
public sealed record EvidenceRecord(
    string ResourceType, string Id, string DocumentId, string Section, string Text);
