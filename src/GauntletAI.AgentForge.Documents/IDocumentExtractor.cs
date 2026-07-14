using GauntletAI.AgentForge.Data.Entities;

namespace GauntletAI.AgentForge.Documents;

/// <summary>
/// Extracts structured, source-cited facts from a clinical document via the vision model, gating the output
/// through the strict schema (Week 2 Core Req 1/2, W2_ARCHITECTURE.md §3). The <c>attach_and_extract</c>
/// flow reads the file and delegates the extraction here.
/// </summary>
public interface IDocumentExtractor
{
    /// <summary>Extracts <paramref name="content"/> as the given <paramref name="documentType"/>.</summary>
    /// <param name="documentType">Which strict schema to extract to.</param>
    /// <param name="content">The raw document bytes (PDF or image).</param>
    /// <param name="mediaType">IANA media type of <paramref name="content"/> (e.g. <c>application/pdf</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validated extraction, or a rejection when the model output fails the schema.</returns>
    Task<DocumentExtractionResult> ExtractAsync(
        ClinicalDocumentType documentType,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken);
}
