using System.Net.Http.Headers;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using Microsoft.Extensions.Configuration;
using Refit;

namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>
/// Shared fixture wiring the real Refit-generated OpenEMR clients against a live QA deployment.
/// Nothing here is mocked (tests/AGENTS.md) - <see cref="AuthApi"/> and <see cref="FhirApi"/> are
/// the actual production clients from GauntletAI.AgentForge.Integration.OpenEmr, pointed at
/// whatever QA endpoint the environment configures.
/// </summary>
public sealed class OpenEmrQaFixture
{
    /// <summary>QA connection details resolved from environment variables.</summary>
    public QaOpenEmrOptions Options { get; }

    /// <summary>Real OAuth2/SMART client against the QA server - no bearer token attached.</summary>
    public IOpenEmrAuthApi AuthApi { get; }

    /// <summary>
    /// Real FHIR client against the QA server, carrying <see cref="QaOpenEmrOptions.TestAccessToken"/>
    /// as its bearer token. Only usable when that optional config is set.
    /// </summary>
    public IOpenEmrFhirApi FhirApi { get; }

    /// <summary>
    /// Real FHIR client against the QA server with no bearer token attached, regardless of
    /// whether <see cref="QaOpenEmrOptions.TestAccessToken"/> is configured - for the
    /// authorization-boundary tests, which need a guaranteed-unauthenticated call.
    /// </summary>
    public IOpenEmrFhirApi UnauthenticatedFhirApi { get; }

    /// <summary>
    /// Real FHIR client carrying <see cref="QaOpenEmrOptions.SecondTestAccessToken"/> as its bearer
    /// token - a second, distinct clinician identity for the cross-identity entitlement tests. Only
    /// usable when that optional config is set.
    /// </summary>
    public IOpenEmrFhirApi SecondFhirApi { get; }

    public OpenEmrQaFixture()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var section = configuration.GetSection(QaOpenEmrOptions.SectionName);

        var baseUrl = section["BaseUrl"];
        var site = section["Site"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(site))
        {
            throw new InvalidOperationException(
                $"QA OpenEMR integration tests require {QaOpenEmrOptions.SectionName}__BaseUrl and " +
                $"{QaOpenEmrOptions.SectionName}__Site environment variables pointing at a real deployed " +
                "QA instance. These tests exercise real external dependencies, not mocks " +
                "(ENGINEERING_STANDARDS.md §8.2 / tests/AGENTS.md) - there is no mock fallback to fail over to.");
        }

        var systemSection = section.GetSection("System");
        var systemClientId = systemSection["ClientId"];
        var systemPrivateKeyPath = systemSection["PrivateKeyPath"];
        var systemKeyId = systemSection["KeyId"];
        var systemScope = systemSection["Scope"];

        var testAccessToken = section["TestAccessToken"];
        if (!string.IsNullOrWhiteSpace(systemClientId) && !string.IsNullOrWhiteSpace(systemPrivateKeyPath))
        {
            // client_credentials + JWT-bearer minting (GitLab issue #22) supersedes a static
            // TestAccessToken - it never expires in practice, unlike a token re-minted by hand
            // through an interactive SMART launch. A configured-but-failed mint throws rather than
            // silently falling back to a possibly-stale static token: that would defeat the point of
            // this fix and mask the real failure (NFR-REL-2's "meaningful check, not an unconditional
            // pass" applied to test config, not just /ready).
            testAccessToken = OpenEmrSystemTokenClient.MintAccessTokenAsync(
                baseUrl,
                site,
                systemClientId,
                systemPrivateKeyPath,
                systemKeyId ?? throw new InvalidOperationException(
                    $"{QaOpenEmrOptions.SectionName}__System__KeyId is required when System__ClientId is set."),
                systemScope ?? throw new InvalidOperationException(
                    $"{QaOpenEmrOptions.SectionName}__System__Scope is required when System__ClientId is set."))
                .GetAwaiter().GetResult();
        }

        Options = new QaOpenEmrOptions
        {
            BaseUrl = baseUrl,
            Site = site,
            TestAccessToken = testAccessToken,
            TestPatientId = section["TestPatientId"],
            TestClientId = section["TestClientId"],
            TestClientSecret = section["TestClientSecret"],
            SecondTestAccessToken = section["SecondTestAccessToken"],
            SecondTestPatientId = section["SecondTestPatientId"],
            SystemClientId = systemClientId,
            SystemPrivateKeyPath = systemPrivateKeyPath,
            SystemKeyId = systemKeyId,
            SystemScope = systemScope,
        };

        var authHttpClient = new HttpClient { BaseAddress = new Uri(Options.BaseUrl) };
        AuthApi = RestService.For<IOpenEmrAuthApi>(authHttpClient);

        var unauthenticatedHttpClient = new HttpClient { BaseAddress = new Uri(Options.BaseUrl) };
        UnauthenticatedFhirApi = RestService.For<IOpenEmrFhirApi>(unauthenticatedHttpClient);

        var fhirHttpClient = new HttpClient { BaseAddress = new Uri(Options.BaseUrl) };
        if (!string.IsNullOrEmpty(Options.TestAccessToken))
        {
            fhirHttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", Options.TestAccessToken);
        }

        FhirApi = RestService.For<IOpenEmrFhirApi>(fhirHttpClient);

        var secondFhirHttpClient = new HttpClient { BaseAddress = new Uri(Options.BaseUrl) };
        if (!string.IsNullOrEmpty(Options.SecondTestAccessToken))
        {
            secondFhirHttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", Options.SecondTestAccessToken);
        }

        SecondFhirApi = RestService.For<IOpenEmrFhirApi>(secondFhirHttpClient);
    }
}
