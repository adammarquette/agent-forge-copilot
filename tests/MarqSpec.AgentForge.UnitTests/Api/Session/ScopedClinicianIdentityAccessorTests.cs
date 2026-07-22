using FluentAssertions;
using MarqSpec.AgentForge.Api.Session;

namespace MarqSpec.AgentForge.UnitTests.Api.Session;

public sealed class ScopedClinicianIdentityAccessorTests
{
    [Fact]
    public void ClinicianIdentity_SetThenRead_ReturnsWhatWasSet()
    {
        var sut = new ScopedClinicianIdentityAccessor { ClinicianIdentity = "dr-jones" };

        sut.ClinicianIdentity.Should().Be("dr-jones");
    }

    [Fact]
    public void ClinicianIdentity_NeverSet_ReturnsNull()
    {
        // A hub invocation that never authenticates the session must never fall back to a
        // stale or default identity being attributed to an access it didn't make.
        var sut = new ScopedClinicianIdentityAccessor();

        sut.ClinicianIdentity.Should().BeNull();
    }
}
