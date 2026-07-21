using FluentAssertions;
using MarqSpec.AgentForge.Api.Session;

namespace MarqSpec.AgentForge.UnitTests.Api.Session;

public sealed class MutableCorrelationIdAccessorTests
{
    [Fact]
    public void CorrelationId_FirstAccess_MintsANonEmptyValue()
    {
        var sut = new MutableCorrelationIdAccessor();

        sut.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CorrelationId_AccessedTwiceWithinTheSameScope_ReturnsTheSameValue()
    {
        // BFF ingress mints one correlation id per request/hub invocation and propagates it to
        // every downstream call (ARCHITECTURE.md §11) - a second mint mid-request would break
        // that reconstruction.
        var sut = new MutableCorrelationIdAccessor();

        var first = sut.CorrelationId;
        var second = sut.CorrelationId;

        second.Should().Be(first);
    }

    [Fact]
    public void CorrelationId_TwoSeparateInstances_MintDifferentValues()
    {
        // Each DI scope (one per request/hub invocation) gets its own instance - this guards that
        // the mint is actually random per scope, not a shared/static counter.
        var first = new MutableCorrelationIdAccessor().CorrelationId;
        var second = new MutableCorrelationIdAccessor().CorrelationId;

        second.Should().NotBe(first);
    }
}
