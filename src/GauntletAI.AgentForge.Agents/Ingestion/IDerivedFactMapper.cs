using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;

namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <summary>
/// Maps a successful <see cref="DocumentExtractionResult"/> into sidecar <see cref="DerivedFact"/>s, each
/// citing the OpenEMR <c>DocumentReference</c> the source was written to. The document-reference id is null
/// when citation resolution is pending — the facts are still persisted, with a pending source id. The
/// concrete mapper (per-schema citation shaping) is a separate unit (Phase 4b).
/// </summary>
public interface IDerivedFactMapper
{
    /// <summary>Shapes the extraction into derived facts with citations.</summary>
    IReadOnlyList<DerivedFact> Map(DocumentExtractionResult extraction, string? documentReferenceId);
}
