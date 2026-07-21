using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Launch;

namespace MarqSpec.AgentForge.UnitTests.Api.Launch;

public sealed class BffOptionsTests
{
    [Fact]
    public void Validate_PathBaseNotSet_ProducesNoErrors()
    {
        // Default/backward-compatible: root-hosted deployment, no reverse-proxy path prefix.
        var options = new BffOptions { PublicBaseUrl = "https://bff.example.invalid" };

        var results = Validate(options);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_PathBaseHasLeadingSlashAndNoTrailingSlash_ProducesNoErrors()
    {
        var options = new BffOptions
        {
            PublicBaseUrl = "https://bff.example.invalid",
            PathBase = "/agentforge",
        };

        var results = Validate(options);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_PathBaseMissingLeadingSlash_ProducesError()
    {
        var options = new BffOptions
        {
            PublicBaseUrl = "https://bff.example.invalid",
            PathBase = "agentforge",
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(BffOptions.PathBase)));
    }

    [Fact]
    public void Validate_PathBaseHasTrailingSlash_ProducesError()
    {
        // A trailing slash would double up against paths that already start with "/" when
        // concatenated (e.g. the session cookie's Path, UsePathBase) - reject it here rather than
        // let a double-slash bug surface downstream.
        var options = new BffOptions
        {
            PublicBaseUrl = "https://bff.example.invalid",
            PathBase = "/agentforge/",
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(BffOptions.PathBase)));
    }

    private static List<ValidationResult> Validate(BffOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
