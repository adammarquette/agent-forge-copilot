using System.Net;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;

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
    public async Task<DocumentWriteResult> WriteAsync(
        DocumentWriteRequest request, CancellationToken cancellationToken = default)
    {
        var part = new ByteArrayPart(request.Content, request.FileName, request.MediaType);
        try
        {
            using var response = await _api.UploadDocumentAsync(
                    _site, request.PatientId, request.CategoryPath, request.EncounterId, part, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return DocumentWriteResult.Written();
            }

            var status = (int)response.StatusCode;
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                OpenEmrDocumentWriterLog.Unauthorized(_logger, status);
                return DocumentWriteResult.Unauthorized(
                    $"OpenEMR rejected the document write ({status}); the admin identity needs the api:oemr scope and patients:docs write ACL.");
            }

            OpenEmrDocumentWriterLog.FailedStatus(_logger, status);
            return DocumentWriteResult.Failed($"OpenEMR document write returned {status}.");
        }
        catch (UnauthenticatedRequestException ex)
        {
            // No admin token was resolvable - a configuration gap, not a transient failure.
            OpenEmrDocumentWriterLog.NoAdminToken(_logger, ex);
            return DocumentWriteResult.Unauthorized("No administrative access token was available for the document write.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Degrade deterministically on any OpenEMR-side/transport failure; cancellation still propagates.
            OpenEmrDocumentWriterLog.FailedException(_logger, ex);
            return DocumentWriteResult.Failed($"OpenEMR document write failed: {ex.GetType().Name}.");
        }
    }
}
