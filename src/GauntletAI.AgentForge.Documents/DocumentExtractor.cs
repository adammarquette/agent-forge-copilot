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
    private readonly ILogger<DocumentExtractor> _logger;

    /// <summary>Creates the extractor.</summary>
    /// <param name="llm">The model provider (must support image/document content).</param>
    /// <param name="logger">Logger (PHI-free).</param>
    public DocumentExtractor(ILlmProvider llm, ILogger<DocumentExtractor> logger)
    {
        _llm = llm;
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

        try
        {
            // Deserialize against the strict, source-generated schema. Missing required members throw
            // (the schema-is-the-gate mechanism); re-serialize the typed value as the canonical payload.
            var canonicalJson = documentType switch
            {
                ClinicalDocumentType.LabPdf =>
                    Canonicalize(json, DocumentExtractionJsonContext.Default.LabExtraction),
                ClinicalDocumentType.IntakeForm =>
                    Canonicalize(json, DocumentExtractionJsonContext.Default.IntakeExtraction),
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

    private static string Canonicalize<T>(string json, JsonTypeInfo<T> typeInfo)
    {
        var value = JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new JsonException("Payload deserialized to null.");
        return JsonSerializer.Serialize(value, typeInfo);
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
