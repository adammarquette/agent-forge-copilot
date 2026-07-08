using GauntletAI.AgentForge.Api.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Test-only route that seeds a real <see cref="PatientSessionContext"/> into the ASP.NET Core
/// session, bypassing the interactive SMART login a fully-automated test can't drive - the same
/// reasoning as <c>QaOpenEmrOptions.TestAccessToken</c> (a real token obtained once out-of-band).
/// Registered only by <see cref="BffQaFixture"/>, never by production <c>Program.cs</c>. Placed
/// after the real pipeline (<paramref name="next"/> runs first) so <c>UseSession()</c> has already
/// run by the time this middleware executes; routing only falls through to it for the one path it
/// owns, which the real app never defines.
/// </summary>
internal sealed class SeedSessionStartupFilter : IStartupFilter
{
    /// <summary>The one path this test-only middleware handles.</summary>
    public const string SeedSessionPath = "/test-only/seed-patient-session";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);

        app.Run(async context =>
        {
            if (context.Request.Method != HttpMethods.Post || context.Request.Path != SeedSessionPath)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await context.Session.LoadAsync(context.RequestAborted).ConfigureAwait(false);
            var session = new PatientSessionContext(
                context.Request.Query["accessToken"].ToString(),
                context.Request.Query["site"].ToString(),
                context.Request.Query["patientId"].ToString());
            context.Session.SavePatientSession(session);
            await context.Session.CommitAsync(context.RequestAborted).ConfigureAwait(false);

            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });
    };
}
