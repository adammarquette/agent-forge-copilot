using FluentAssertions;
using MarqSpec.AgentForge.Data;

namespace MarqSpec.AgentForge.UnitTests.Data;

/// <summary>
/// Drives <see cref="ContentHash"/> — the idempotency key for document ingestion (W2_ARCHITECTURE.md §4).
/// It must be deterministic (same bytes → same key), collision-sensitive (any change → a different key), and
/// a canonical lowercase-hex SHA-256.
/// </summary>
public sealed class ContentHashTests
{
    [Fact]
    public void Compute_ForSameContent_IsDeterministic()
    {
        byte[] content = [1, 2, 3, 4, 5];

        ContentHash.Compute(content).Should().Be(ContentHash.Compute(content));
    }

    [Fact]
    public void Compute_ForDifferentContent_Differs()
    {
        ContentHash.Compute([1, 2, 3]).Should().NotBe(ContentHash.Compute([1, 2, 4]));
    }

    [Fact]
    public void Compute_ForKnownInput_MatchesSha256LowercaseHex()
    {
        // SHA-256 of the empty input, canonical lowercase hex.
        ContentHash.Compute([]).Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }
}
