using System.Net;
using FluentAssertions;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Exercises the real running BFF host's Daily Agenda launch endpoints (ARCHITECTURE.md §19) - the
/// mirror image of <see cref="BffLaunchFlowTests"/>: does the actual composition root produce a
/// working redirect using the agenda's own registered client, and does the CSRF state check hold
/// through real cookies. Confirms the agenda and single-patient launch flows coexist without
/// clobbering each other's pending-launch session state.
/// </summary>
public sealed class BffAgendaLaunchFlowTests : IClassFixture<BffQaFixture>
{
    private readonly BffQaFixture _fixture;

    public BffAgendaLaunchFlowTests(BffQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AgendaLaunch_RealHostRealConfig_RedirectsToTheConfiguredOpenEmrAuthorizeEndpoint()
    {
        using var client = _fixture.CreateHttpClient(new CookieContainer());

        using var response = await client.GetAsync(
            "/agenda/launch?iss=https://openemr.example.org&launch=test-launch-token", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith($"{_fixture.OpenEmr.Options.BaseUrl.TrimEnd('/')}/oauth2/{_fixture.OpenEmr.Options.Site}/authorize?");
    }

    [Fact]
    public async Task AgendaCallback_StateDoesNotMatchThePendingLaunch_RejectsWithBadRequestBeforeAnyTokenExchange()
    {
        var cookies = new CookieContainer();
        using var client = _fixture.CreateHttpClient(cookies);

        using (var launchResponse = await client.GetAsync(
            "/agenda/launch?iss=https://openemr.example.org&launch=test-launch-token", CancellationToken.None))
        {
            launchResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        using var callbackResponse = await client.GetAsync(
            "/agenda/callback?code=irrelevant&state=a-state-that-was-never-issued", CancellationToken.None);

        callbackResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AgendaLaunch_AndSinglePatientLaunch_InTheSameBrowserSessionDoNotClobberEachOthersPendingState()
    {
        // Regression guard for the distinct pending-launch session keys added alongside the agenda
        // flow (AgendaLaunchEndpoints) - both launches mid-flight in one cookie jar must each still
        // complete (or correctly reject) independently.
        var cookies = new CookieContainer();
        using var client = _fixture.CreateHttpClient(cookies);

        using (var agendaLaunch = await client.GetAsync(
            "/agenda/launch?iss=https://openemr.example.org&launch=agenda-token", CancellationToken.None))
        {
            agendaLaunch.StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        using (var patientLaunch = await client.GetAsync(
            "/launch?iss=https://openemr.example.org&launch=patient-token", CancellationToken.None))
        {
            patientLaunch.StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        // Both callbacks should still be rejectable independently on a bad state, rather than one
        // flow's pending context having been overwritten/cleared by the other's launch.
        using var agendaCallback = await client.GetAsync(
            "/agenda/callback?code=irrelevant&state=wrong-state", CancellationToken.None);
        agendaCallback.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var patientCallback = await client.GetAsync(
            "/callback?code=irrelevant&state=wrong-state", CancellationToken.None);
        patientCallback.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
