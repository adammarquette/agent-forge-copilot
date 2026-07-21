using System.Net;
using FluentAssertions;

namespace MarqSpec.AgentForge.IntegrationTests.Api;

/// <summary>
/// Exercises the real running BFF host's Daily Agenda roster/drill-down endpoints
/// (ARCHITECTURE.md §19) - specifically the authorization boundary
/// (<see cref="AgendaRosterGate"/>) end to end through real cookies/session middleware, not just
/// the unit-tested pure function in isolation. Seeded via <see cref="SeedSessionStartupFilter"/>
/// rather than a real SMART launch, since these cases (401 unauthenticated, 403 out-of-roster)
/// don't depend on the token being real - only <c>GET /agenda</c>'s happy path (a real OpenEMR
/// Appointment fetch) needs the agenda OAuth client registration deferred to the QA pass
/// (agent-forge-copilot#56).
/// </summary>
public sealed class BffAgendaEndpointsTests : IClassFixture<BffQaFixture>
{
    private readonly BffQaFixture _fixture;

    public BffAgendaEndpointsTests(BffQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetAgenda_NoAgendaSession_ReturnsUnauthorized()
    {
        using var client = _fixture.CreateHttpClient(new CookieContainer());

        using var response = await client.GetAsync("/agenda", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SelectPatient_NoAgendaSession_ReturnsUnauthorized()
    {
        using var client = _fixture.CreateHttpClient(new CookieContainer());

        using var response = await client.PostAsync("/agenda/select-patient?patientId=patient-1", content: null, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SelectPatient_PatientNotInTheSeededRoster_ReturnsForbidden()
    {
        var cookies = await _fixture.SeedAuthenticatedAgendaSessionAsync(["patient-1", "patient-2"], CancellationToken.None);
        using var client = _fixture.CreateHttpClient(cookies);

        using var response = await client.PostAsync(
            "/agenda/select-patient?patientId=someone-elses-patient", content: null, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SelectPatient_PatientInTheSeededRoster_RedirectsToTheChatPath()
    {
        var cookies = await _fixture.SeedAuthenticatedAgendaSessionAsync(["patient-1"], CancellationToken.None);
        using var client = _fixture.CreateHttpClient(cookies);

        using var response = await client.PostAsync(
            "/agenda/select-patient?patientId=patient-1", content: null, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/index.html");
    }
}
