using System.Text;

namespace MarqSpec.AgentForge.Documents;

/// <summary>
/// Locates a citation's verbatim quote among a page's word rectangles and returns their union as a normalized
/// top-left <c>[x, y, w, h]</c> (FR-CITE-2, gitlab#110). Pure and whitespace-insensitive: it finds the
/// contiguous run of words whose letters spell the quote (spaces dropped, case-folded), then unions their
/// boxes. Returns null when the quote isn't found on the cited page, so the caller falls back to the model's
/// estimate or page-level rather than boxing the wrong region.
/// </summary>
public static class CitationBoundingBoxResolver
{
    /// <summary>Resolves the exact box for <paramref name="quote"/> on <paramref name="page"/>, or null if not found.</summary>
    public static double[]? Resolve(IReadOnlyList<PdfWord> words, int page, string quote)
    {
        var target = Normalize(quote);
        if (target.Length == 0)
        {
            return null;
        }

        var pageWords = new List<PdfWord>();
        foreach (var word in words)
        {
            if (word.PageNumber == page)
            {
                pageWords.Add(word);
            }
        }

        // Grow a contiguous run from each start until its folded text reaches the quote's length: an exact
        // match unions that run's boxes; an overshoot without a match abandons this start for the next.
        for (var start = 0; start < pageWords.Count; start++)
        {
            var run = new StringBuilder();
            for (var end = start; end < pageWords.Count; end++)
            {
                run.Append(Normalize(pageWords[end].Text));
                if (run.Length < target.Length)
                {
                    continue;
                }

                if (run.Length == target.Length && run.ToString() == target)
                {
                    return Union(pageWords, start, end);
                }

                break;
            }
        }

        return null;
    }

    private static double[] Union(List<PdfWord> words, int from, int to)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (var k = from; k <= to; k++)
        {
            var word = words[k];
            minX = Math.Min(minX, word.X);
            minY = Math.Min(minY, word.Y);
            maxX = Math.Max(maxX, word.X + word.Width);
            maxY = Math.Max(maxY, word.Y + word.Height);
        }

        return [minX, minY, maxX - minX, maxY - minY];
    }

    // Fold to a comparison key: drop whitespace, lower-case. Punctuation/digits are kept so "3.5-5.1" and
    // "mmol/L" still align with how the model quotes them.
    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
