using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace GauntletAI.AgentForge.Documents;

/// <summary>
/// <see cref="IPdfWordReader"/> over UglyToad.PdfPig. Reads glyph rectangles from the PDF's text layer and
/// normalizes them to a top-left <c>[0,1]</c> box. A PDF with no text layer (a scan), or one that is encrypted
/// or malformed, yields an empty list (logged at Debug, PHI-free) so citation boxes degrade to the model's
/// estimate. reference: gitlab#110.
/// </summary>
public sealed class PdfPigWordReader : IPdfWordReader
{
    private readonly ILogger<PdfPigWordReader> _logger;

    /// <summary>Creates the reader.</summary>
    public PdfPigWordReader(ILogger<PdfPigWordReader> logger) => _logger = logger;

    /// <inheritdoc />
    public IReadOnlyList<PdfWord> ReadWords(ReadOnlyMemory<byte> pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf.ToArray());
            var words = new List<PdfWord>();
            foreach (var page in document.GetPages())
            {
                if (page.Width <= 0 || page.Height <= 0)
                {
                    continue;
                }

                foreach (var word in page.GetWords())
                {
                    var box = word.BoundingBox;
                    // PdfPig's origin is bottom-left in points; flip Y and normalize both axes to the page.
                    var x = box.Left / page.Width;
                    var width = (box.Right - box.Left) / page.Width;
                    var y = (page.Height - box.Top) / page.Height;
                    var height = (box.Top - box.Bottom) / page.Height;
                    words.Add(new PdfWord(
                        page.Number, word.Text, Clamp01(x), Clamp01(y), Clamp01(width), Clamp01(height)));
                }
            }

            return words;
        }
        catch (Exception ex)
        {
            DocumentExtractorLog.PdfWordsUnreadable(_logger, ex);
            return [];
        }
    }

    private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
}
