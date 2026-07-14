using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;

namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <inheritdoc />
public sealed class DerivedFactMapper : IDerivedFactMapper
{
    /// <inheritdoc />
    public IReadOnlyList<DerivedFact> Map(DocumentExtractionResult extraction, string? documentReferenceId) =>
        throw new NotImplementedException();
}
