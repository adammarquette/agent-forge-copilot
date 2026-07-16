using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.Api.Evidence;

/// <summary>
/// The Week 2 multimodal-evidence endpoint: upload a clinical document + ask a question, run the
/// supervisor/worker graph, and return a grounded, verified answer with its handoff trace and evidence
/// (W2_ARCHITECTURE.md §6). It extracts from the uploaded document and retrieves from the guideline corpus,
/// so it makes no user-scoped FHIR call — but it still consumes LLM + retrieval quota, so it is gated by the
/// BFF session like the rest of the launched app (401 without a launch), not left open. This is what removes
/// its earlier outlier status (a public, unauthenticated endpoint). reference: agent-forge-copilot#96, #105.
/// </summary>
public static class EvidenceEndpoints
{
    /// <summary>Maps the Week 2 evidence endpoints: <c>POST /evidence/ask</c> and the source-document fetch.</summary>
    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/evidence/ask", HandleAskAsync).DisableAntiforgery();
        endpoints.MapGet("/evidence/document/{documentId}", HandleGetDocumentAsync);
        return endpoints;
    }

    /// <summary>
    /// <c>GET /evidence/document/{documentId}</c> — streams a source document's bytes for the click-to-source
    /// overlay's production path (FR-CITE-2, gitlab#109). Session-gated like the rest of the app; the fetch
    /// runs as the launched clinician, and OpenEMR scopes FHIR <c>Binary</c> to that patient, so a session can
    /// only read its own patient's documents (authorization below the model, not in this handler).
    /// </summary>
    private static async Task<IResult> HandleGetDocumentAsync(
        HttpContext httpContext, string documentId,
        IOpenEmrFhirClient fhirClient, IScopedAccessTokenProvider tokenProvider)
    {
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        if (httpContext.Session.TryGetPatientSession() is not { } session)
        {
            return Results.Unauthorized();
        }

        // Set the AsyncLocal token once; the FHIR client's auth handler reads it back for the Binary call.
        tokenProvider.AccessToken = session.AccessToken;
        var document = await fhirClient.GetBinaryAsync(session.Site, documentId, httpContext.RequestAborted).ConfigureAwait(false);
        return document is null
            ? Results.NotFound()
            : Results.File(document.Content, document.ContentType);
    }

    private static async Task<IResult> HandleAskAsync(HttpContext httpContext, IEvidenceAgentSupervisor supervisor)
    {
        // Gate on the BFF session, same as /agenda and /patient: the endpoint burns LLM + retrieval quota, so
        // access requires a launch even though the flow itself makes no user-scoped FHIR call (#96, #105).
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        if (httpContext.Session.TryGetPatientSession() is null)
        {
            return Results.Unauthorized();
        }

        var request = httpContext.Request;
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart/form-data with a 'question' and an optional 'file'.");
        }

        var form = await request.ReadFormAsync(httpContext.RequestAborted).ConfigureAwait(false);

        var question = form["question"].ToString();
        if (string.IsNullOrWhiteSpace(question))
        {
            return Results.BadRequest("'question' is required.");
        }

        var patientId = form["patientId"].ToString();
        if (string.IsNullOrWhiteSpace(patientId))
        {
            patientId = "demo";
        }

        PendingDocument? document = null;
        var file = form.Files["file"];
        if (file is { Length: > 0 })
        {
            if (!TryParseDocType(form["docType"].ToString(), out var documentType))
            {
                return Results.BadRequest("'docType' must be 'lab_pdf' or 'intake_form' when a file is attached.");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, httpContext.RequestAborted).ConfigureAwait(false);
            document = new PendingDocument(documentType, buffer.ToArray(), file.ContentType);
        }

        var agentRequest = new EvidenceAgentRequest { PatientId = patientId, Question = question, Document = document };
        var result = await supervisor.RunAsync(agentRequest, httpContext.RequestAborted).ConfigureAwait(false);
        return Results.Ok(ToPayload(result));
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

    private static EvidenceResponsePayload ToPayload(EvidenceAgentResult result) => new(
        result.Answer,
        [.. result.Handoffs.Select(h => new HandoffPayload(h.From, h.To, h.Reason))],
        [.. result.Evidence.Select(e => new EvidencePayload(e.DocumentId, e.Section, e.ChunkId, e.Text, e.Score))],
        result.SafetyFlags.Count,
        result.SuppressedClaims.Count,
        result.ExtractedFactsJson,
        result.DocumentCitations);
}
