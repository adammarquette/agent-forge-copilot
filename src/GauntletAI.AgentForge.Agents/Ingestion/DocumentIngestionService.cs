using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Observability;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <summary>
/// Orchestrates document ingestion (W2_ARCHITECTURE.md §4): content-hash idempotency → extract → persist the
/// derived facts, each citing the OpenEMR <c>DocumentReference</c> the front desk already uploaded to. The
/// source document is authoritative in OpenEMR (uploaded natively), so the sidecar never writes it — it only
/// derives facts. Runs pre-visit so the clinician's turn just reads ready facts. Degrades deterministically
/// (a schema-gate rejection persists nothing) and cancellation propagates.
/// </summary>
public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IDocumentExtractor _extractor;
    private readonly IDerivedFactStore _store;
    private readonly IDerivedFactMapper _mapper;
    private readonly IAgentForgeMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DocumentIngestionService> _logger;

    /// <summary>Creates the ingestion orchestrator.</summary>
    public DocumentIngestionService(
        IDocumentExtractor extractor,
        IDerivedFactStore store,
        IDerivedFactMapper mapper,
        IAgentForgeMetrics metrics,
        TimeProvider timeProvider,
        ILogger<DocumentIngestionService> logger)
    {
        _extractor = extractor;
        _store = store;
        _mapper = mapper;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DocumentIngestionResult> IngestAsync(
        DocumentIngestionRequest request, CancellationToken cancellationToken = default)
    {
        var startTimestamp = _timeProvider.GetTimestamp();
        var contentHash = ContentHash.Compute(request.Content);

        // 1. Idempotency: the same bytes are never extracted or recorded twice (W2-D3).
        var existing = await _store.FindByContentHashAsync(contentHash, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            DocumentIngestionServiceLog.AlreadyIngested(_logger);
            _metrics.RecordDocumentIngestion("already_ingested", _timeProvider.GetElapsedTime(startTimestamp));
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
            _metrics.RecordDocumentIngestion("extraction_rejected", _timeProvider.GetElapsedTime(startTimestamp));
            return DocumentIngestionResult.ExtractionRejected(contentHash, extraction.RejectionReason);
        }

        // 3. Persist the derived facts, each citing the source DocumentReference the front desk uploaded to.
        // CreatedAt/IngestedAt are app-set (no DB default); stamp one timestamp across the document and facts.
        var now = _timeProvider.GetUtcNow();
        var facts = _mapper.Map(extraction, request.DocumentReferenceId);
        foreach (var fact in facts)
        {
            fact.CreatedAt = now;
        }

        var document = new IngestedDocument
        {
            PatientId = request.PatientId,
            DocumentType = request.DocumentType,
            ContentHash = contentHash,
            OpenEmrDocumentReferenceId = request.DocumentReferenceId,
            IngestedAt = now,
            DerivedFacts = [.. facts],
        };
        await _store.AddAsync(document, cancellationToken).ConfigureAwait(false);

        DocumentIngestionServiceLog.Ingested(_logger, facts.Count);
        _metrics.RecordDocumentIngestion("ingested", _timeProvider.GetElapsedTime(startTimestamp));
        return DocumentIngestionResult.Ingested(contentHash, request.DocumentReferenceId, facts.Count);
    }
}
