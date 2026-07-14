using GauntletAI.AgentForge.Data;
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
    public Task<DocumentIngestionResult> IngestAsync(
        DocumentIngestionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
