using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr;

public sealed class OpenEmrOptionsTests
{
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

    private static List<ValidationResult> Validate(OpenEmrOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
