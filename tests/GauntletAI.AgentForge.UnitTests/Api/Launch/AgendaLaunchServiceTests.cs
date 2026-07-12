using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Api.Launch;
using GauntletAI.AgentForge.Integration.OpenEmr;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.UnitTests.Api.Launch;

public sealed class AgendaLaunchServiceTests
{
    private readonly IOpenEmrAuthClient _authClient = A.Fake<IOpenEmrAuthClient>();
    private readonly ILogger<AgendaLaunchService> _logger = A.Fake<ILogger<AgendaLaunchService>>();
    private readonly AgendaLaunchService _sut;

    public AgendaLaunchServiceTests()
    {
        var openEmrOptions = Options.Create(new OpenEmrOptions
        {
            BaseUrl = "https://openemr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["patient/patient.read", "launch/patient"],
        });
        var agendaOptions = Options.Create(new AgendaOpenEmrOptions
        {
            ClientId = "sidecar-agenda-client",
            Scopes = ["user/Patient.read", "user/encounter.read", "user/Appointment.read"],
        });
        var bffOptions = Options.Create(new BffOptions { PublicBaseUrl = "https://sidecar.example.org" });
        _sut = new AgendaLaunchService(_authClient, openEmrOptions, agendaOptions, bffOptions, _logger);
    }

    [Fact]
    public void BeginLaunch_ValidLaunchToken_BuildsAuthorizeUrlAgainstTheConfiguredSiteUsingTheAgendaClientId()
    {
        // Confirms the agenda flow authenticates as its own registered client, not
        // OpenEmrOptions.ClientId - reusing the single-patient launch's client id would silently
        // narrow the granted scopes (ScopeRepository::finalizeScopes, ARCHITECTURE.md §19.2).
        var (authorizeUrl, _) = _sut.BeginLaunch(launchToken: null);

        authorizeUrl.ToString().Should().StartWith("https://openemr.example.org/oauth2/default/authorize?");
        authorizeUrl.ToString().Should().Contain("client_id=sidecar-agenda-client");
        authorizeUrl.ToString().Should().Contain(Uri.EscapeDataString("https://sidecar.example.org/agenda/callback"));
    }

    [Fact]
    public void BeginLaunch_ValidLaunchToken_RequestsTheAgendaScopesNotTheSinglePatientScopes()
    {
        var (authorizeUrl, _) = _sut.BeginLaunch(launchToken: null);
        var url = authorizeUrl.ToString();

        url.Should().Contain(Uri.EscapeDataString("user/Patient.read"));
        url.Should().Contain(Uri.EscapeDataString("user/encounter.read"));
        url.Should().Contain(Uri.EscapeDataString("user/Appointment.read"));
        url.Should().NotContain(Uri.EscapeDataString("patient/patient.read"));
    }

    [Fact]
    public void BeginLaunch_ValidLaunchToken_SendsTheFhirBaseAsAudNotTheBareServerBaseUrl()
    {
        var (authorizeUrl, _) = _sut.BeginLaunch(launchToken: null);

        authorizeUrl.ToString().Should().Contain(
            $"aud={Uri.EscapeDataString("https://openemr.example.org/apis/default/fhir")}");
    }

    [Fact]
    public void BeginLaunch_CalledTwice_MintsADifferentStateAndCodeVerifierEachTime()
    {
        var (_, first) = _sut.BeginLaunch(launchToken: null);
        var (_, second) = _sut.BeginLaunch(launchToken: null);

        first.State.Should().NotBe(second.State);
        first.CodeVerifier.Should().NotBe(second.CodeVerifier);
    }

    [Fact]
    public async Task CompleteLaunchAsync_StateDoesNotMatchThePendingLaunch_ThrowsRatherThanExchangingTheCode()
    {
        var (_, pending) = _sut.BeginLaunch(launchToken: null);

        var act = () => _sut.CompleteLaunchAsync("auth-code", "a-different-state", pending, CancellationToken.None);

        await act.Should().ThrowAsync<AgendaLaunchException>();
        A.CallTo(_authClient).MustNotHaveHappened();
    }

    [Fact]
    public async Task CompleteLaunchAsync_MatchingStateAndValidCodeWithNoPatientClaim_ReturnsAnAgendaSessionWithNoPatientId()
    {
        // The whole point of the agenda launch: it must succeed with NO launch patient context,
        // the opposite of SmartLaunchService's single-patient requirement.
        var (_, pending) = _sut.BeginLaunch(launchToken: null);
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                "default", "auth-code", "https://sidecar.example.org/agenda/callback", "sidecar-agenda-client",
                pending.CodeVerifier, null, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "user/Patient.read", null, Patient: null, null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                "default", "access-token-abc", "sidecar-agenda-client", A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(true, "user/Patient.read", "sidecar-agenda-client", null, "dr-jones", null)));

        var result = await _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        result.AccessToken.Should().Be("access-token-abc");
        result.Site.Should().Be("default");
        result.ClinicianIdentity.Should().Be("dr-jones");
    }

    [Fact]
    public async Task CompleteLaunchAsync_TokenResponseCarriesAnUnexpectedPatientClaim_StillSucceedsButLogs()
    {
        // A patient claim on a main-tab launch would mean the fork's INTENT_MAIN_TAB launch didn't
        // behave as expected - worth a warning for diagnosis, but not a reason to fail the whole
        // agenda launch outright (UC-5: degrade/report, don't fail closed on a soft anomaly).
        A.CallTo(() => _logger.IsEnabled(LogLevel.Warning)).Returns(true);
        var (_, pending) = _sut.BeginLaunch(launchToken: null);
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "user/Patient.read", null, "unexpected-patient-123", null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(true, "user/Patient.read", "sidecar-agenda-client", null, "dr-jones", "unexpected-patient-123")));

        var result = await _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        result.ClinicianIdentity.Should().Be("dr-jones");
        A.CallTo(_logger).Where(call => call.Method.Name == "Log")
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CompleteLaunchAsync_IntrospectionReturnsNoSubject_ThrowsRatherThanStartingAnUnauditableSession()
    {
        var (_, pending) = _sut.BeginLaunch(launchToken: null);
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "user/Patient.read", null, Patient: null, null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(true, "user/Patient.read", "sidecar-agenda-client", null, Subject: null, null)));

        var act = () => _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        await act.Should().ThrowAsync<AgendaLaunchException>();
    }

    [Fact]
    public async Task CompleteLaunchAsync_IntrospectionReturnsInactiveWithASubject_ThrowsRatherThanStartingARevokedSession()
    {
        var (_, pending) = _sut.BeginLaunch(launchToken: null);
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "user/Patient.read", null, Patient: null, null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(false, "user/Patient.read", "sidecar-agenda-client", null, "dr-jones", null)));

        var act = () => _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        await act.Should().ThrowAsync<AgendaLaunchException>();
    }
}
