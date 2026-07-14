using GauntletAI.AgentForge.Data.Entities;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Documents;

/// <summary>Source-generated log messages for <see cref="DocumentExtractor"/> (CA1848). PHI-free.</summary>
internal static partial class DocumentExtractorLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Extraction produced no JSON payload for {DocumentType}")]
    public static partial void NoJsonPayload(ILogger logger, ClinicalDocumentType documentType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Extraction failed schema validation for {DocumentType}")]
    public static partial void SchemaValidationFailed(ILogger logger, ClinicalDocumentType documentType, Exception exception);
}
