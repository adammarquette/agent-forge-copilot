using FluentAssertions;
using GauntletAI.AgentForge.Documents;

namespace GauntletAI.AgentForge.UnitTests.Documents;

/// <summary>
/// Unit tests for <see cref="CitationBoundingBoxResolver"/> — the pure matcher behind the pixel-accurate
/// click-to-source boxes (FR-CITE-2, gitlab#110). Given the page's word rectangles (from a digital PDF) and a
/// citation's verbatim quote, it locates the contiguous run of words spelling that quote and returns their
/// union as a normalized top-left <c>[x, y, w, h]</c>. Guarded behavior: whitespace-insensitive matching,
/// per-page scoping, and a clean null when the quote isn't found (so the caller degrades, never guesses).
/// </summary>
public sealed class CitationBoundingBoxResolverTests
{
    // A lab row "Potassium 5.6 mmol/L" laid out left-to-right at the same vertical band on page 1.
    private static readonly PdfWord[] Row =
    [
        new(1, "Potassium", 0.10, 0.20, 0.15, 0.03),
        new(1, "5.6", 0.30, 0.20, 0.05, 0.03),
        new(1, "mmol/L", 0.40, 0.20, 0.08, 0.03),
    ];

    [Fact]
    public void Resolve_QuoteMatchesRow_ReturnsUnionOfTheWordBoxes()
    {
        var box = CitationBoundingBoxResolver.Resolve(Row, page: 1, quote: "Potassium 5.6 mmol/L");

        box.Should().NotBeNull();
        box!.Should().HaveCount(4);
        box[0].Should().BeApproximately(0.10, 1e-9);           // left-most x
        box[1].Should().BeApproximately(0.20, 1e-9);           // top y
        box[2].Should().BeApproximately(0.38, 1e-9);           // width: (0.40 + 0.08) - 0.10
        box[3].Should().BeApproximately(0.03, 1e-9);           // row height
    }

    [Fact]
    public void Resolve_IsWhitespaceInsensitive()
    {
        // Extra/irregular spacing in the model's quote must still match the same words.
        var box = CitationBoundingBoxResolver.Resolve(Row, page: 1, quote: "  Potassium   5.6  mmol/L ");

        box.Should().NotBeNull();
        box![0].Should().BeApproximately(0.10, 1e-9);
        box[2].Should().BeApproximately(0.38, 1e-9);
    }

    [Fact]
    public void Resolve_MatchesASubRunOfTheRow()
    {
        // A shorter quote should box only the words it covers, not the whole row.
        var box = CitationBoundingBoxResolver.Resolve(Row, page: 1, quote: "Potassium 5.6");

        box.Should().NotBeNull();
        box![0].Should().BeApproximately(0.10, 1e-9);
        box[2].Should().BeApproximately(0.25, 1e-9);           // (0.30 + 0.05) - 0.10
    }

    [Fact]
    public void Resolve_ScopesToTheCitedPage()
    {
        PdfWord[] twoPages =
        [
            new(1, "Potassium", 0.10, 0.90, 0.15, 0.03), new(1, "5.6", 0.30, 0.90, 0.05, 0.03),
            new(2, "Potassium", 0.10, 0.20, 0.15, 0.03), new(2, "5.6", 0.30, 0.20, 0.05, 0.03),
        ];

        var box = CitationBoundingBoxResolver.Resolve(twoPages, page: 2, quote: "Potassium 5.6");

        box.Should().NotBeNull();
        box![1].Should().BeApproximately(0.20, 1e-9);          // the page-2 copy (y=0.20), not page-1's y=0.90
    }

    [Fact]
    public void Resolve_QuoteNotOnPage_ReturnsNull()
    {
        CitationBoundingBoxResolver.Resolve(Row, page: 1, quote: "Sodium 139").Should().BeNull();
    }

    [Fact]
    public void Resolve_NoWordsOrEmptyQuote_ReturnsNull()
    {
        CitationBoundingBoxResolver.Resolve([], page: 1, quote: "Potassium").Should().BeNull();
        CitationBoundingBoxResolver.Resolve(Row, page: 1, quote: "   ").Should().BeNull();
    }
}
