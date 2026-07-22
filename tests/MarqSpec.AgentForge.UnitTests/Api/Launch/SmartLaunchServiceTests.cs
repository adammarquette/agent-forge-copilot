using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Launch;
using MarqSpec.AgentForge.Integration.OpenEmr;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.UnitTests.Api.Launch;

public sealed class SmartLaunchServiceTests
{
    private readonly IOpenEmrAuthClient _authClient = A.Fake<IOpenEmrAuthClient>();
    private readonly ILogger<SmartLaunchService> _logger = A.Fake<ILogger<SmartLaunchService>>();
    private readonly SmartLaunchService _sut;

    public SmartLaunchServiceTests()
    {
        var openEmrOptions = Options.Create(new OpenEmrOptions
        {
            BaseUrl = "https://openemr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["patient/patient.read", "launch/patient"],
        });
        var bffOptions = Options.Create(new BffOptions { PublicBaseUrl = "https://sidecar.example.org" });
        _sut = new SmartLaunchService(_authClient, openEmrOptions, bffOptions, _logger);
    }

    [Fact]
    public void BeginLaunch_ValidLaunchToken_BuildsAuthorizeUrlAgainstTheConfiguredSite()
    {
        var (authorizeUrl, _) = _sut.BeginLaunch("launch-token-abc");

        authorizeUrl.ToString().Should().StartWith("https://openemr.example.org/oauth2/default/authorize?");
        authorizeUrl.ToString().Should().Contain("client_id=sidecar-client");
        authorizeUrl.ToString().Should().Contain("launch=launch-token-abc");
        authorizeUrl.ToString().Should().Contain(Uri.EscapeDataString("https://sidecar.example.org/callback"));
    }

    [Fact]
    public void BeginLaunch_ValidLaunchToken_SendsTheFhirBaseAsAudNotTheBareServerBaseUrl()
    {
        // OpenEMR rejects a bare-server-base aud with "invalid_request - Aud parameter did not
        // match authorized server" (confirmed live) - it expects the FHIR base
        // ({BaseUrl}/apis/{Site}/fhir), the same value every other real client in this repo
        // (tools/MintQaIdentityToken, the QA test fixtures) already sends successfully.
        var (authorizeUrl, _) = _sut.BeginLaunch("launch-token-abc");

        authorizeUrl.ToString().Should().Contain(
            $"aud={Uri.EscapeDataString("https://openemr.example.org/apis/default/fhir")}");
    }

    [Fact]
    public void BeginLaunch_CalledTwice_MintsADifferentStateAndCodeVerifierEachTime()
    {
        // Guards CSRF protection and PKCE: a reused state/verifier across launches would let one
        // launch's callback be replayed against another.
        var (_, first) = _sut.BeginLaunch("launch-token-abc");
        var (_, second) = _sut.BeginLaunch("launch-token-abc");

        first.State.Should().NotBe(second.State);
        first.CodeVerifier.Should().NotBe(second.CodeVerifier);
    }

    [Fact]
    public async Task CompleteLaunchAsync_StateDoesNotMatchThePendingLaunch_ThrowsRatherThanExchangingTheCode()
    {
        var (_, pending) = _sut.BeginLaunch("launch-token-abc");

        var act = () => _sut.CompleteLaunchAsync("auth-code", "a-different-state", pending, CancellationToken.None);

        await act.Should().ThrowAsync<SmartLaunchException>();
        A.CallTo(_authClient).MustNotHaveHappened();
    }

    [Fact]
    public async Task CompleteLaunchAsync_MatchingStateAndValidCode_ExchangesUsingThePkceVerifierFromBeginLaunch()
    {
        var (_, pending) = _sut.BeginLaunch("launch-token-abc");
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                "default", "auth-code", "https://sidecar.example.org/callback", "sidecar-client",
                pending.CodeVerifier, null, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "patient/patient.read", null, "patient-123", null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                "default", "access-token-abc", "sidecar-client", A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(true, "patient/patient.read", "sidecar-client", null, "dr-jones", "patient-123")));

        var result = await _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        result.AccessToken.Should().Be("access-token-abc");
        result.Site.Should().Be("default");
        result.PatientId.Should().Be("patient-123");
        result.ClinicianIdentity.Should().Be("dr-jones");
    }

    [Fact]
    public async Task CompleteLaunchAsync_TokenResponseCarriesNoPatientClaim_ThrowsRatherThanStartingAnUnscopedSession()
    {
        // This product is single-patient-scoped for its entire session lifetime (FR-CHAT-3) - a
        // token with no launch patient context can never be turned into a valid PatientSessionContext.
        var (_, pending) = _sut.BeginLaunch("launch-token-abc");
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "patient/patient.read", null, Patient: null, null)));

        var act = () => _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        await act.Should().ThrowAsync<SmartLaunchException>();
    }

    [Fact]
    public async Task CompleteLaunchAsync_IntrospectionReturnsNoSubject_ThrowsRatherThanStartingAnUnauditableSession()
    {
        // FR-AUTH-4: every patient-data access must be attributable to who accessed it - a session
        // with no clinician identity could never be audited, so it must not be allowed to start.
        var (_, pending) = _sut.BeginLaunch("launch-token-abc");
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "patient/patient.read", null, "patient-123", null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(true, "patient/patient.read", "sidecar-client", null, Subject: null, "patient-123")));

        var act = () => _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        await act.Should().ThrowAsync<SmartLaunchException>();
    }

    [Fact]
    public async Task CompleteLaunchAsync_IntrospectionReturnsInactiveWithASubject_ThrowsRatherThanStartingARevokedSession()
    {
        // FR-AUTH-4: a token that OpenEMR reports as no-longer-active (revoked or expired) must not
        // be allowed to start a session, even if introspection still carries a subject claim - this
        // fork's introspection endpoint has a history of not behaving per RFC 7662 (#44, #47), so
        // "subject present but active:false" is a plausible real state, not just a spec nicety.
        var (_, pending) = _sut.BeginLaunch("launch-token-abc");
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "patient/patient.read", null, "patient-123", null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(false, "patient/patient.read", "sidecar-client", null, "dr-jones", "patient-123")));

        var act = () => _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        await act.Should().ThrowAsync<SmartLaunchException>();
    }

    [Fact]
    public async Task CompleteLaunchAsync_IntrospectionReturnsNoSubject_LogsActiveAndClientIdForDiagnosis()
    {
        // A missing subject is otherwise indistinguishable in the thrown exception between "OpenEMR
        // rejected our client" (active:false) and "active but no subject for some other reason" -
        // this warning is what lets an operator tell the two apart from logs alone, with no PHI.
        A.CallTo(() => _logger.IsEnabled(LogLevel.Warning)).Returns(true);
        var (_, pending) = _sut.BeginLaunch("launch-token-abc");
        A.CallTo(() => _authClient.ExchangeAuthorizationCodeAsync(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TokenResponse("access-token-abc", "Bearer", 3600, "patient/patient.read", null, "patient-123", null)));
        A.CallTo(() => _authClient.IntrospectAsync(
                A<string>._, A<string>._, A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new IntrospectionResponse(false, "patient/patient.read", "sidecar-client", null, Subject: null, "patient-123")));

        var act = () => _sut.CompleteLaunchAsync("auth-code", pending.State, pending, CancellationToken.None);

        await act.Should().ThrowAsync<SmartLaunchException>();
        A.CallTo(_logger).Where(call => call.Method.Name == "Log")
            .MustHaveHappenedOnceExactly();
    }
}
