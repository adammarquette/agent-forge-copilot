using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Integration.OpenEmr.Standard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <inheritdoc />
public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IDocumentExtractor _extractor;
    private readonly IOpenEmrDocumentWriter _writer;
    private readonly IDocumentReferenceResolver _resolver;
    private readonly IDerivedFactStore _store;
    private readonly IDerivedFactMapper _mapper;
    private readonly string _categoryPath;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DocumentIngestionService> _logger;

    /// <summary>Creates the ingestion orchestrator.</summary>
    public DocumentIngestionService(
        IDocumentExtractor extractor,
        IOpenEmrDocumentWriter writer,
        IDocumentReferenceResolver resolver,
        IDerivedFactStore store,
        IDerivedFactMapper mapper,
        IOptions<DocumentIngestionOptions> options,
        TimeProvider timeProvider,
        ILogger<DocumentIngestionService> logger)
    {
        _extractor = extractor;
        _writer = writer;
        _resolver = resolver;
        _store = store;
        _mapper = mapper;
        _categoryPath = options.Value.CategoryPath;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DocumentIngestionResult> IngestAsync(
        DocumentIngestionRequest request, CancellationToken cancellationToken = default)
    {
        var contentHash = ContentHash.Compute(request.Content);

        // 1. Idempotency: the same bytes are never written or recorded twice (W2-D3).
        var existing = await _store.FindByContentHashAsync(contentHash, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            DocumentIngestionServiceLog.AlreadyIngested(_logger);
            return DocumentIngestionResult.AlreadyIngested(
                contentHash, existing.OpenEmrDocumentReferenceId, existing.DerivedFacts.Count);
        }

        // 2. Extract; a schema-gate rejection persists nothing ("vision without invention").
        var extraction = await _extractor
            .ExtractAsync(request.DocumentType, request.Content, request.MediaType, cancellationToken)
            .ConfigureAwait(false);
        if (!extraction.Succeeded)
        {
            DocumentIngestionServiceLog.ExtractionRejected(_logger);
            return DocumentIngestionResult.ExtractionRejected(contentHash, extraction.RejectionReason);
        }

        // 3. Snapshot BEFORE the write so the citation resolver can diff for the newly-appeared reference.
        var knownBefore = await _resolver.SnapshotAsync(request.PatientId, cancellationToken).ConfigureAwait(false);

        // 4. Write the source document to OpenEMR (authoritative there). A failure persists nothing.
        var write = await _writer.WriteAsync(
            new DocumentWriteRequest
            {
                PatientId = request.PatientId,
                FileName = request.FileName,
                Content = request.Content,
                MediaType = request.MediaType,
                CategoryPath = _categoryPath,
                EncounterId = request.EncounterId,
            },
            cancellationToken).ConfigureAwait(false);
        if (!write.Succeeded)
        {
            DocumentIngestionServiceLog.WriteFailed(_logger, write.Status.ToString());
            return DocumentIngestionResult.WriteFailed(contentHash, write.Detail);
        }

        // 5. Resolve the DocumentReference id for citation; null leaves the citation pending (still persisted).
        var citation = await _resolver.ResolveNewAsync(request.PatientId, knownBefore, cancellationToken)
            .ConfigureAwait(false);
        if (citation is null)
        {
            DocumentIngestionServiceLog.CitationPending(_logger);
        }

        // 6. Persist the sidecar-authoritative facts with lineage to the source document.
        var facts = _mapper.Map(extraction, citation?.Id);
        var document = new IngestedDocument
        {
            PatientId = request.PatientId,
            DocumentType = request.DocumentType,
            ContentHash = contentHash,
            OpenEmrDocumentReferenceId = citation?.Id,
            IngestedAt = _timeProvider.GetUtcNow(),
            DerivedFacts = [.. facts],
        };
        await _store.AddAsync(document, cancellationToken).ConfigureAwait(false);

        DocumentIngestionServiceLog.Ingested(_logger, facts.Count);
        return DocumentIngestionResult.Ingested(contentHash, citation?.Id, facts.Count);
    }
}
