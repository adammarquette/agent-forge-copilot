using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Test-only routes that seed a real <see cref="PatientSessionContext"/> or
/// <see cref="AgendaSessionContext"/> (plus its roster) into the ASP.NET Core session, bypassing
/// the interactive SMART login a fully-automated test can't drive - the same reasoning as
/// <c>QaOpenEmrOptions.TestAccessToken</c> (a real token obtained once out-of-band). Registered
/// only by <see cref="BffQaFixture"/>, never by production <c>Program.cs</c>. Placed after the
/// real pipeline (<paramref name="next"/> runs first) so <c>UseSession()</c> has already run by
/// the time this middleware executes; routing only falls through to it for the paths it owns,
/// which the real app never defines.
/// </summary>
internal sealed class SeedSessionStartupFilter : IStartupFilter
{
    /// <summary>Seeds a <see cref="PatientSessionContext"/>.</summary>
    public const string SeedSessionPath = "/test-only/seed-patient-session";

    /// <summary>Seeds an <see cref="AgendaSessionContext"/> and (optionally) its roster.</summary>
    public const string SeedAgendaSessionPath = "/test-only/seed-agenda-session";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);

        app.Run(async context =>
        {
            if (context.Request.Method != HttpMethods.Post)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (context.Request.Path == SeedSessionPath)
            {
                await SeedPatientSessionAsync(context).ConfigureAwait(false);
            }
            else if (context.Request.Path == SeedAgendaSessionPath)
            {
                await SeedAgendaSessionAsync(context).ConfigureAwait(false);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });
    };

    private static async Task SeedPatientSessionAsync(HttpContext context)
    {
        await context.Session.LoadAsync(context.RequestAborted).ConfigureAwait(false);
        var session = new PatientSessionContext(
            context.Request.Query["accessToken"].ToString(),
            context.Request.Query["site"].ToString(),
            context.Request.Query["patientId"].ToString(),
            context.Request.Query["clinicianIdentity"].ToString());
        context.Session.SavePatientSession(session);
        await context.Session.CommitAsync(context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task SeedAgendaSessionAsync(HttpContext context)
    {
        await context.Session.LoadAsync(context.RequestAborted).ConfigureAwait(false);
        var session = new AgendaSessionContext(
            context.Request.Query["accessToken"].ToString(),
            context.Request.Query["site"].ToString(),
            context.Request.Query["clinicianIdentity"].ToString());
        context.Session.SaveAgendaSession(session);

        var rosterPatientIds = context.Request.Query["rosterPatientIds"].ToString();
        if (!string.IsNullOrEmpty(rosterPatientIds))
        {
            context.Session.SaveAgendaRoster(rosterPatientIds.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }

        await context.Session.CommitAsync(context.RequestAborted).ConfigureAwait(false);
    }
}
