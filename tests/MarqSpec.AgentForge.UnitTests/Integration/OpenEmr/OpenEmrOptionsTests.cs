using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr;
using Microsoft.Extensions.Configuration;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr;

public sealed class OpenEmrOptionsTests
{
    [Fact]
    public void Validate_ScopesNeverBoundFromConfiguration_ProducesErrorRatherThanThrowing()
    {
        // Every other test here constructs OpenEmrOptions via object-initializer syntax, which
        // enforces `required` at compile time - Scopes is never actually null in those tests. The
        // `required` keyword gives no such runtime guarantee for reflection-based IConfiguration
        // binding: a config section with no "Scopes" key at all leaves the property genuinely
        // null. This is the gap that let Validate()'s unguarded Scopes.Count throw a
        // NullReferenceException in production (confirmed live: HealthEndpointReadinessTests'
        // fixture, which has no reason to configure OpenEmrAgenda, crashed the whole host at
        // startup - MarqSpec.AgentForge.Api.Launch.AgendaOpenEmrOptions carries the identical
        // pattern and was where this was actually caught).
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenEmr:BaseUrl"] = "https://emr.example.org",
                ["OpenEmr:Site"] = "default",
                ["OpenEmr:ClientId"] = "sidecar-client",
                // Deliberately no OpenEmr:Scopes:* keys at all.
            })
            .Build();
        var options = configuration.GetSection(OpenEmrOptions.SectionName).Get<OpenEmrOptions>()!;

        var act = () => Validate(options);

        act.Should().NotThrow();
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenEmrOptions.Scopes)));
    }

    [Fact]
    public void Validate_AllRequiredFieldsPresentAndHttpsBaseUrl_ProducesNoErrors()
    {
        var options = new OpenEmrOptions
        {
            BaseUrl = "https://emr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["patient/patient.read", "launch"],
        };

        var results = Validate(options);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_HttpBaseUrlWithoutInsecureOverride_ProducesError()
    {
        var options = new OpenEmrOptions
        {
            BaseUrl = "http://emr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["launch"],
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenEmrOptions.BaseUrl)));
    }

    [Fact]
    public void Validate_HttpBaseUrlWithInsecureOverrideSet_ProducesNoErrors()
    {
        var options = new OpenEmrOptions
        {
            BaseUrl = "http://localhost:8300",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["launch"],
            AllowInsecureHttpForLocalDevelopment = true,
        };

        var results = Validate(options);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyScopes_ProducesError()
    {
        var options = new OpenEmrOptions
        {
            BaseUrl = "https://emr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = [],
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenEmrOptions.Scopes)));
    }

    [Fact]
    public void Validate_MalformedBaseUrl_ProducesError()
    {
        var options = new OpenEmrOptions
        {
            BaseUrl = "not-a-url",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["launch"],
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenEmrOptions.BaseUrl)));
    }

    [Fact]
    public void Validate_MissingSite_ProducesError()
    {
        var options = new OpenEmrOptions
        {
            BaseUrl = "https://emr.example.org",
            Site = string.Empty,
            ClientId = "sidecar-client",
            Scopes = ["launch"],
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenEmrOptions.Site)));
    }

    [Fact]
    public void FhirTimeouts_Defaults_AreTunedForSlowOpenEmr()
    {
        // Regression (issue #80): staging OpenEMR routinely takes 4-8s per FHIR call and the
        // framework's 10s attempt default trips under the Daily Agenda's parallel fan-out. These
        // defaults give the OpenEMR client room to complete slow-but-successful calls.
        var options = new OpenEmrOptions
        {
            BaseUrl = "https://emr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["launch"],
        };

        options.FhirAttemptTimeoutSeconds.Should().Be(30);
        options.FhirTotalRequestTimeoutSeconds.Should().Be(90);
    }

    [Fact]
    public void Validate_NonPositiveFhirAttemptTimeout_ProducesError()
    {
        var options = new OpenEmrOptions
        {
            BaseUrl = "https://emr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["launch"],
            FhirAttemptTimeoutSeconds = 0,
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenEmrOptions.FhirAttemptTimeoutSeconds)));
    }

    [Fact]
    public void Validate_FhirTotalTimeoutNotGreaterThanAttempt_ProducesError()
    {
        // The standard resilience handler requires the total budget to strictly exceed a single
        // attempt, or it throws at startup - validate here so a misconfiguration fails fast.
        var options = new OpenEmrOptions
        {
            BaseUrl = "https://emr.example.org",
            Site = "default",
            ClientId = "sidecar-client",
            Scopes = ["launch"],
            FhirAttemptTimeoutSeconds = 30,
            FhirTotalRequestTimeoutSeconds = 30,
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenEmrOptions.FhirTotalRequestTimeoutSeconds)));
    }

    private static List<ValidationResult> Validate(OpenEmrOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
