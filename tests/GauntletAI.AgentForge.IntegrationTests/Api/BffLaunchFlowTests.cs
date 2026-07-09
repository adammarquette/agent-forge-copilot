using System.Net;
using FluentAssertions;

namespace GauntletAI.AgentForge.IntegrationTests.Api;

/// <summary>
/// Exercises the real running BFF host's SMART launch endpoints - contract-drift and
/// wiring coverage a unit test of <c>SmartLaunchService</c> alone can't provide: does the actual
/// composition root (config binding, DI, session middleware) produce a working redirect, and does
/// the CSRF state check hold through real cookies rather than an in-memory fake.
/// </summary>
public sealed class BffLaunchFlowTests : IClassFixture<BffQaFixture>
{
    private readonly BffQaFixture _fixture;

    public BffLaunchFlowTests(BffQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Launch_RealHostRealConfig_RedirectsToTheConfiguredOpenEmrAuthorizeEndpoint()
    {
        using var client = _fixture.CreateHttpClient(new CookieContainer());

        using var response = await client.GetAsync(
            "/launch?iss=https://openemr.example.org&launch=test-launch-token", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith($"{_fixture.OpenEmr.Options.BaseUrl.TrimEnd('/')}/oauth2/{_fixture.OpenEmr.Options.Site}/authorize?");
        location.Should().Contain("launch=test-launch-token");
    }

    [Fact]
    public async Task Callback_StateDoesNotMatchThePendingLaunch_RejectsWithBadRequestBeforeAnyTokenExchange()
    {
        // Proves the CSRF boundary end to end through the real session cookie, not just the
        // unit-tested SmartLaunchService logic in isolation.
        var cookies = new CookieContainer();
        using var client = _fixture.CreateHttpClient(cookies);

        using (var launchResponse = await client.GetAsync(
            "/launch?iss=https://openemr.example.org&launch=test-launch-token", CancellationToken.None))
        {
            launchResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        using var callbackResponse = await client.GetAsync(
            "/callback?code=irrelevant&state=a-state-that-was-never-issued", CancellationToken.None);

        callbackResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
