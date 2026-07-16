using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents.Extraction;
using GauntletAI.AgentForge.Llm;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Documents;

/// <summary>
/// Default <see cref="IDocumentExtractor"/>: sends the document to the vision model behind
/// <see cref="ILlmProvider"/>, then validates the model's output against the strict schema. Output that
/// fails the schema is rejected at the boundary and nothing is persisted (W2_ARCHITECTURE.md §3, W2-D6).
/// </summary>
public sealed class DocumentExtractor : IDocumentExtractor
{
    private const string PdfMediaType = "application/pdf";

    private readonly ILlmProvider _llm;
    private readonly IPdfWordReader _pdfReader;
    private readonly ILogger<DocumentExtractor> _logger;

    /// <summary>Creates the extractor.</summary>
    /// <param name="llm">The model provider (must support image/document content).</param>
    /// <param name="pdfReader">Reads PDF word geometry to make citation boxes exact (gitlab#110).</param>
    /// <param name="logger">Logger (PHI-free).</param>
    public DocumentExtractor(ILlmProvider llm, IPdfWordReader pdfReader, ILogger<DocumentExtractor> logger)
    {
        _llm = llm;
        _pdfReader = pdfReader;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DocumentExtractionResult> ExtractAsync(
        ClinicalDocumentType documentType,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var base64 = Convert.ToBase64String(content.Span);
        LlmContent documentBlock = mediaType.Equals(PdfMediaType, StringComparison.OrdinalIgnoreCase)
            ? new LlmDocumentContent(mediaType, base64)
            : new LlmImageContent(mediaType, base64);

        var request = new LlmRequest(
            SystemPrompt: ExtractionPrompts.SystemFor(documentType),
            Messages: [new LlmMessage(LlmRole.User, [documentBlock, new LlmTextContent(ExtractionPrompts.UserInstruction)])],
            Tools: null,
            MaxOutputTokens: 8192);

        var response = await _llm.CompleteAsync(request, cancellationToken);

        var json = ExtractJsonObject(response.Content);
        if (json is null)
        {
            DocumentExtractorLog.NoJsonPayload(_logger, documentType);
            return DocumentExtractionResult.Rejected(documentType, "Model returned no JSON object.", response.Usage);
        }

        // Exact citation geometry from the PDF's own glyph rectangles (gitlab#110): overrides the model's
        // estimated boxes where the quote is found. Empty for non-PDF or scanned/unreadable input, in which
        // case the model's estimate (or null) stands.
        var words = mediaType.Equals(PdfMediaType, StringComparison.OrdinalIgnoreCase)
            ? _pdfReader.ReadWords(content)
            : [];

        try
        {
            // Deserialize against the strict, source-generated schema. Missing required members throw
            // (the schema-is-the-gate mechanism); resolve exact boxes, then re-serialize the canonical payload.
            var canonicalJson = documentType switch
            {
                ClinicalDocumentType.LabPdf =>
                    Serialize(ResolveLabBoxes(Deserialize(json, DocumentExtractionJsonContext.Default.LabExtraction), words),
                        DocumentExtractionJsonContext.Default.LabExtraction),
                ClinicalDocumentType.IntakeForm =>
                    Serialize(ResolveIntakeBoxes(Deserialize(json, DocumentExtractionJsonContext.Default.IntakeExtraction), words),
                        DocumentExtractionJsonContext.Default.IntakeExtraction),
                _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown document type."),
            };

            return DocumentExtractionResult.Ok(documentType, canonicalJson, response.Usage);
        }
        catch (JsonException ex)
        {
            DocumentExtractorLog.SchemaValidationFailed(_logger, documentType, ex);
            return DocumentExtractionResult.Rejected(
                documentType, $"Extracted JSON failed schema validation: {ex.Message}", response.Usage);
        }
    }

    private static T Deserialize<T>(string json, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Deserialize(json, typeInfo) ?? throw new JsonException("Payload deserialized to null.");

    private static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo) => JsonSerializer.Serialize(value, typeInfo);

    private static LabExtraction ResolveLabBoxes(LabExtraction extraction, IReadOnlyList<PdfWord> words) =>
        words.Count == 0
            ? extraction
            : extraction with { Tests = [.. extraction.Tests.Select(t => t with { Citation = ResolveCitation(t.Citation, words) })] };

    private static IntakeExtraction ResolveIntakeBoxes(IntakeExtraction extraction, IReadOnlyList<PdfWord> words) =>
        words.Count == 0
            ? extraction
            : extraction with
            {
                CurrentMedications = [.. extraction.CurrentMedications.Select(m => m with { Citation = ResolveCitation(m.Citation, words) })],
                Citation = ResolveCitation(extraction.Citation, words),
            };

    // Replace the model's estimated box with the exact one from the PDF when the quote is located; otherwise
    // keep what the model gave (an estimate, or null -> page-level).
    private static ExtractionCitation ResolveCitation(ExtractionCitation citation, IReadOnlyList<PdfWord> words)
    {
        var box = CitationBoundingBoxResolver.Resolve(words, citation.Page, citation.Quote);
        return box is null ? citation : citation with { BoundingBox = box };
    }

    /// <summary>Isolates the outermost JSON object from model text, tolerating stray prose or code fences.</summary>
    private static string? ExtractJsonObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : null;
    }
}
