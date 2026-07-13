using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.Api.Patient;

/// <summary>
/// The per-patient landing page's one data endpoint: the launched patient's identity + FHIR
/// reachability, for the confirmation page served at <c>index.html</c>. Thin by design - the same
/// shape as <c>AgendaEndpoints</c>, leaving the work in <see cref="PatientContextService"/>.
/// </summary>
public static class PatientEndpoints
{
    /// <summary>Maps <c>GET /patient</c> - the launched-patient context confirmation.</summary>
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/patient", HandleGetPatientAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleGetPatientAsync(HttpContext httpContext, PatientContextService service)
    {
        await httpContext.Session.LoadAsync(httpContext.RequestAborted).ConfigureAwait(false);
        var session = httpContext.Session.TryGetPatientSession();
        if (session is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.BuildAsync(session, httpContext.RequestAborted).ConfigureAwait(false);
        return Results.Ok(ToPayload(result));
    }

    private static PatientContextResponsePayload ToPayload(PatientContextResult result) => new(
        result.PatientId,
        result.DisplayName,
        result.BirthDate,
        result.Gender,
        result.Demographics,
        result.Problems,
        result.Medications,
        result.Allergies);
}
