using GauntletAI.AgentForge.Agents.Ingestion;
using GauntletAI.AgentForge.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.Api.Ingestion;

/// <summary>
/// The document-ingestion endpoint (W2_ARCHITECTURE.md §4). The front desk uploads through OpenEMR's own
/// Documents workflow; the <c>oe-module-agentforge</c> ingestion cron then calls this endpoint with the
/// document's content + its OpenEMR <c>DocumentReference</c> id, pre-visit, so the clinician's turn only reads
/// ready facts.
/// <para>
/// Trust model (W2-D17, single-environment): this route is reachable ONLY over the Railway private network -
/// the reverse proxy front door does not route <c>/documents/ingest</c> (only <c>/agentforge/*</c> reaches the
/// sidecar), and the sidecar has no public domain of its own. It carries no clinician authority (it derives
/// facts, it makes no user-scoped FHIR call), so it authenticates by trusted origin rather than an OpenEMR
/// token. A shared-secret header is the tracked hardening follow-up (defense-in-depth against in-project
/// callers / accidental public exposure); intentionally out of scope for the MVP.
/// </para>
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
        IDocumentIngestionService ingestionService)
    {
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
