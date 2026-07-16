using GauntletAI.AgentForge.Agents;

namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// Default <see cref="IEvidenceTool"/>: runs hybrid retrieval over the guideline corpus and projects each
/// snippet to a citable <c>[Guideline/&lt;chunkId&gt;]</c> record, so the model can ground and cite an answer in
/// the corpus (FR-RAG-2, gitlab#117). The retrieval itself (dense + sparse + rerank) and its degradation live
/// in <see cref="IEvidenceRetriever"/>; this tool only adapts the snippets to the citation shape.
/// </summary>
public sealed class EvidenceTool(IEvidenceRetriever retriever) : IEvidenceTool
{
    private const int DefaultTopK = 5;

    /// <inheritdoc />
    public async Task<EvidenceResult> GetAsync(string query, CancellationToken cancellationToken)
    {
        var snippets = await retriever.RetrieveAsync(query, DefaultTopK, cancellationToken).ConfigureAwait(false);
        var records = snippets
            .Select(snippet => new EvidenceRecord("Guideline", snippet.ChunkId, snippet.DocumentId, snippet.Section, snippet.Text))
            .ToList();

        return new EvidenceResult(records);
    }
}
