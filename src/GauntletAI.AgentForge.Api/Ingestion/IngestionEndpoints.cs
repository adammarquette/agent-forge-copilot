using GauntletAI.AgentForge.Agents.Ingestion;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.Api.Ingestion;

/// <summary>
/// The front-office document-ingestion endpoint (W2_ARCHITECTURE.md §4): upload a source document for the
/// current patient; the sidecar writes it back to OpenEMR and persists the derived facts. Thin by design —
/// it runs the ingestion under the launched user's token (same custody as chat/agenda), so OpenEMR's
/// front-office <c>patients:docs</c> ACL is the write authority; a user without it gets a clean failure.
/// </summary>
public static class IngestionEndpoints
{
    /// <summary>Maps <c>POST /ingest/document</c> — the front-office upload path.</summary>
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ingest/document", HandleIngestAsync).DisableAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> HandleIngestAsync(
        HttpContext httpContext,
        IScopedAccessTokenProvider tokenProvider,
        IDocumentIngestionService ingestionService)
    {
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var session = httpContext.Session.TryGetPatientSession();
        if (session is null)
        {
            return Results.Unauthorized();
        }

        var request = httpContext.Request;
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart/form-data with 'file' and 'docType'.");
        }

        var form = await request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);

        var file = form.Files["file"];
        if (file is not { Length: > 0 })
        {
            return Results.BadRequest("'file' is required.");
        }

        if (!TryParseDocType(form["docType"].ToString(), out var documentType))
        {
            return Results.BadRequest("'docType' must be 'lab_pdf' or 'intake_form'.");
        }

        var encounterId = form["eid"].ToString();

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, httpContext.RequestAborted).ConfigureAwait(false);

        // Run the write/read under the launched user's token; OpenEMR's ACL is the front-office authority.
        tokenProvider.AccessToken = session.AccessToken;

        var result = await ingestionService.IngestAsync(
            new DocumentIngestionRequest
            {
                PatientId = session.PatientId,
                DocumentType = documentType,
                Content = buffer.ToArray(),
                MediaType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FileName = string.IsNullOrEmpty(file.FileName) ? "upload" : file.FileName,
                EncounterId = string.IsNullOrWhiteSpace(encounterId) ? null : encounterId,
            },
            httpContext.RequestAborted).ConfigureAwait(false);

        return result.Status switch
        {
            DocumentIngestionStatus.Ingested or DocumentIngestionStatus.AlreadyIngested => Results.Ok(ToPayload(result)),
            DocumentIngestionStatus.ExtractionRejected => Results.UnprocessableEntity(ToPayload(result)),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway),
        };
    }

    private static bool TryParseDocType(string value, out ClinicalDocumentType documentType)
    {
        switch (value)
        {
            case "lab_pdf":
                documentType = ClinicalDocumentType.LabPdf;
                return true;
            case "intake_form":
                documentType = ClinicalDocumentType.IntakeForm;
                return true;
            default:
                documentType = default;
                return false;
        }
    }

    private static IngestionResponsePayload ToPayload(DocumentIngestionResult result) => new(
        result.Status.ToString(),
        result.DocumentReferenceId,
        result.CitationPending,
        result.FactCount,
        result.Detail);
}
