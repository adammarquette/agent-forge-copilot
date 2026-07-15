using GauntletAI.AgentForge.Agents.Ingestion;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Integration.OpenEmr;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Api.Ingestion;

/// <summary>
/// The document-ingestion endpoint (W2_ARCHITECTURE.md §4). The front desk uploads through OpenEMR's own
/// Documents workflow; the <c>oe-module-agentforge</c> upload hook then calls this endpoint with the
/// document's content + its OpenEMR <c>DocumentReference</c> id, carrying the uploading user's access token
/// as a bearer. This runs pre-visit, so the clinician's turn only reads ready facts. The token is introspected
/// per call (transient — validated, never stored): the transient-token model, not a sidecar-held identity.
/// </summary>
public static class IngestionEndpoints
{
    /// <summary>Maps <c>POST /documents/ingest</c> — the OpenEMR-triggered ingestion call.</summary>
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/documents/ingest", HandleIngestAsync).DisableAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> HandleIngestAsync(
        HttpContext httpContext,
        IOpenEmrAuthClient authClient,
        IOptions<OpenEmrOptions> openEmrOptions,
        IDocumentIngestionService ingestionService)
    {
        // Authenticate the caller by introspecting the uploading user's bearer token - a valid, active token
        // is the authority; no session, no stored credential. reference: documentation/W2_ARCHITECTURE.md §4
        if (!TryReadBearerToken(httpContext, out var token))
        {
            return Results.Unauthorized();
        }

        var options = openEmrOptions.Value;
        var introspection = await authClient
            .IntrospectAsync(options.Site, token, options.ClientId, options.ClientSecret, httpContext.RequestAborted)
            .ConfigureAwait(false);
        if (!introspection.Active)
        {
            return Results.Unauthorized();
        }

        var request = httpContext.Request;
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart/form-data with 'file', 'patientId', 'documentReferenceId', 'docType'.");
        }

        var form = await request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);

        var file = form.Files["file"];
        if (file is not { Length: > 0 })
        {
            return Results.BadRequest("'file' is required.");
        }

        var patientId = form["patientId"].ToString();
        var documentReferenceId = form["documentReferenceId"].ToString();
        if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(documentReferenceId))
        {
            return Results.BadRequest("'patientId' and 'documentReferenceId' are required.");
        }

        if (!TryParseDocType(form["docType"].ToString(), out var documentType))
        {
            return Results.BadRequest("'docType' must be 'lab_pdf' or 'intake_form'.");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, httpContext.RequestAborted).ConfigureAwait(false);

        var result = await ingestionService.IngestAsync(
            new DocumentIngestionRequest
            {
                PatientId = patientId,
                DocumentReferenceId = documentReferenceId,
                DocumentType = documentType,
                Content = buffer.ToArray(),
                MediaType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
            },
            httpContext.RequestAborted).ConfigureAwait(false);

        return result.Status switch
        {
            DocumentIngestionStatus.Ingested or DocumentIngestionStatus.AlreadyIngested => Results.Ok(ToPayload(result)),
            _ => Results.UnprocessableEntity(ToPayload(result)),
        };
    }

    private static bool TryReadBearerToken(HttpContext httpContext, out string token)
    {
        token = string.Empty;
        var header = httpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (string.IsNullOrEmpty(header) || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = header[prefix.Length..].Trim();
        return token.Length > 0;
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
        result.FactCount,
        result.Detail);
}
