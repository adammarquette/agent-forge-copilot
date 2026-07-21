using System.Text.Json;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Auth;

/// <summary>
/// Exercises the actual JSON deserialization of <see cref="IntrospectionResponse"/> (not the mocked
/// Refit interface - see <c>OpenEmrAuthClientIntrospectionTests</c> for that layer). Confirmed live
/// against the real deployed QA server (2026-07-10): this OpenEMR fork's <c>/introspect</c> returns
/// <c>exp</c> as a PHP <c>DateTime</c>-shaped JSON object (<c>{"date":..,"timezone_type":..,"timezone":..}</c>),
/// not the RFC 7662 Unix-seconds number - System.Text.Json's default strict record deserialization
/// throws on this and takes down the *entire* SMART launch callback (a 500, masked until now behind
/// a never-registered OAuth client - see SmartLaunchService's aud-parameter fix for the same pattern).
/// </summary>
public sealed class IntrospectionResponseJsonTests
{
    [Fact]
    public void Deserialize_ExpAsRfc7662UnixSecondsNumber_ParsesExpiresAtUnixSeconds()
    {
        const string json = """{"active":true,"exp":1234567890}""";

        var result = JsonSerializer.Deserialize<IntrospectionResponse>(json);

        result!.ExpiresAtUnixSeconds.Should().Be(1234567890);
    }

    [Fact]
    public void Deserialize_ExpAsPhpDateTimeObject_DoesNotThrowAndLeavesExpiresAtUnixSecondsNull()
    {
        const string json = """
            {"active":true,"exp":{"date":"2026-07-10 02:07:46.940000","timezone_type":3,"timezone":"UTC"}}
            """;

        var act = () => JsonSerializer.Deserialize<IntrospectionResponse>(json);

        act.Should().NotThrow();
        act().ExpiresAtUnixSeconds.Should().BeNull();
    }
}
