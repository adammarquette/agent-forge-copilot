using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <inheritdoc />
public sealed class OpenEmrDocumentWriter : IOpenEmrDocumentWriter
{
    private readonly IOpenEmrDocumentApi _api;
    private readonly string _site;
    private readonly ILogger<OpenEmrDocumentWriter> _logger;

    /// <summary>Creates the writer.</summary>
    public OpenEmrDocumentWriter(
        IOpenEmrDocumentApi api, IOptions<OpenEmrOptions> options, ILogger<OpenEmrDocumentWriter> logger)
    {
        _api = api;
        _site = options.Value.Site;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<DocumentWriteResult> WriteAsync(
        DocumentWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
