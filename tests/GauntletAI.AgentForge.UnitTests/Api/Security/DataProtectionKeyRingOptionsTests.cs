using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using GauntletAI.AgentForge.Api.Security;

namespace GauntletAI.AgentForge.UnitTests.Api.Security;

public sealed class DataProtectionKeyRingOptionsTests
{
    [Fact]
    public void KeyRingPath_DefaultsToEmpty_SoLocalDevAndTestsStayEphemeral()
    {
        // Empty is the safe default: no shared key store configured means fall back to the
        // framework's in-memory key ring, which is fine for a laptop or a unit test but must be
        // overridden in any deployed environment (see Validate + Program.cs wiring).
        var options = new DataProtectionKeyRingOptions();

        options.KeyRingPath.Should().BeEmpty();
        options.ApplicationName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_KeyRingPathEmpty_ProducesNoErrors()
    {
        var results = Validate(new DataProtectionKeyRingOptions { KeyRingPath = string.Empty });

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_KeyRingPathAbsolute_ProducesNoErrors()
    {
        // The deployed shape: a persistent volume mounted at a rooted path.
        var absolute = OperatingSystem.IsWindows() ? @"C:\keys" : "/keys";

        var results = Validate(new DataProtectionKeyRingOptions { KeyRingPath = absolute });

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_KeyRingPathRelative_ProducesError()
    {
        // A relative path would resolve against the container's working directory - which is not
        // persisted and differs per process - silently reintroducing the ephemeral-key bug this
        // whole option exists to prevent. Reject it loudly at startup instead.
        var results = Validate(new DataProtectionKeyRingOptions { KeyRingPath = "keys" });

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(DataProtectionKeyRingOptions.KeyRingPath)));
    }

    private static List<ValidationResult> Validate(DataProtectionKeyRingOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
