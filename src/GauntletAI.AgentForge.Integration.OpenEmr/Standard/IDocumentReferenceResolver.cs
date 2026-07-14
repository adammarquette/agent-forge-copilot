using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <summary>
/// Resolves the OpenEMR <c>DocumentReference</c> for a document the sidecar just wrote via the standard REST
/// API. That write returns no id (reference: agent-forge#43) and the FHIR record carries no content hash, so
/// the id is recovered by a <b>set-diff</b>: snapshot the patient's existing DocumentReference ids before the
/// write, then find the single one that appeared after. It never guesses — an empty or ambiguous diff
/// resolves to <c>null</c> so the caller degrades (persist the fact with a pending citation) rather than cite
/// the wrong document.
/// </summary>
public interface IDocumentReferenceResolver
{
    /// <summary>Snapshots the patient's current DocumentReference ids. Call <b>before</b> writing the new document.</summary>
    Task<IReadOnlySet<string>> SnapshotAsync(string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the single DocumentReference that appeared since <paramref name="knownBefore"/>. Returns
    /// <c>null</c> when nothing new appeared (write not yet visible or failed) or when more than one did
    /// (ambiguous) — the caller then leaves the citation pending rather than guessing.
    /// </summary>
    Task<ClinicalSourceRef?> ResolveNewAsync(
        string patientId, IReadOnlySet<string> knownBefore, CancellationToken cancellationToken = default);
}
