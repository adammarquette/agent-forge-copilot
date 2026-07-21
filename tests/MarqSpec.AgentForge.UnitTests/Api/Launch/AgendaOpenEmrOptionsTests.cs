using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Launch;
using Microsoft.Extensions.Configuration;

namespace MarqSpec.AgentForge.UnitTests.Api.Launch;

public sealed class AgendaOpenEmrOptionsTests
{
    [Fact]
    public void Validate_ClientIdAndScopesPresent_ProducesNoErrors()
    {
        var options = new AgendaOpenEmrOptions
        {
            ClientId = "agenda-client",
            Scopes = ["user/Patient.read"],
        };

        var results = Validate(options);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyScopes_ProducesError()
    {
        var options = new AgendaOpenEmrOptions
        {
            ClientId = "agenda-client",
            Scopes = [],
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(AgendaOpenEmrOptions.Scopes)));
    }

    [Fact]
    public void Validate_ScopesNeverBoundFromConfiguration_ProducesErrorRatherThanThrowing()
    {
        // The regression this class was actually caught by: `required` is a compile-time-only
        // guarantee, not a runtime one under IConfiguration binding via reflection. A config
        // section with no Scopes key at all (a legitimate state - not every environment/fixture
        // configures the Daily Agenda feature) leaves the property genuinely null, not an empty
        // list. Confirmed live in production: this exact gap threw NullReferenceException at host
        // startup for HealthEndpointReadinessTests, which has no reason to set OpenEmrAgenda config.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenEmrAgenda:ClientId"] = "agenda-client",
                // Deliberately no OpenEmrAgenda:Scopes:* keys at all.
            })
            .Build();
        var options = configuration.GetSection(AgendaOpenEmrOptions.SectionName).Get<AgendaOpenEmrOptions>()!;

        var act = () => Validate(options);

        act.Should().NotThrow();
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains(nameof(AgendaOpenEmrOptions.Scopes)));
    }

    [Fact]
    public void Validate_MissingClientId_ProducesError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenEmrAgenda:Scopes:0"] = "user/Patient.read",
                // Deliberately no OpenEmrAgenda:ClientId.
            })
            .Build();
        var options = configuration.GetSection(AgendaOpenEmrOptions.SectionName).Get<AgendaOpenEmrOptions>()!;

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(AgendaOpenEmrOptions.ClientId)));
    }

    private static List<ValidationResult> Validate(AgendaOpenEmrOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
