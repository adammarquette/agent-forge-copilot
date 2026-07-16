using FluentAssertions;
using GauntletAI.AgentForge.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Documents;

/// <summary>
/// Unit tests for <see cref="PdfPigWordReader"/> against a tiny fixture PDF (200x200 pt) with "Alpha" drawn
/// near the top and "Omega" near the bottom. Guards the coordinate normalization — especially the bottom-left
/// (PDF) to top-left (overlay) Y flip, whose reversal would land every citation box on the wrong row — and the
/// graceful empty result on bytes that aren't a readable PDF.
/// </summary>
public sealed class PdfPigWordReaderTests
{
    // 200x200pt PDF: "Alpha" at PDF-(10,180) near the top, "Omega" at PDF-(120,12) near the bottom.
    private const string FixturePdfBase64 =
        "JVBERi0xLjcKJYGBgYEKCjYgMCBvYmoKPDwKL0ZpbHRlciAvRmxhdGVEZWNvZGUKL0xlbmd0aCAxMjEKPj4Kc3RyZWFtCnicdcw9" +
        "CgJRDATgfk6RWhCT8DbJA7HwDwsbIRcQWcVFixXx/LpbijIwxTB8PZYJpiGPC2a79vZqn9fTcepcowR7VBKlPEML5R4yXoXkU8GU" +
        "d8yL2MrZwmRB2SEn2CQO6P/J1YtaaGPxW1Ye90He2toa8y/5DWu1J8kKZW5kc3RyZWFtCmVuZG9iagoKNyAwIG9iago8PAovRmls" +
        "dGVyIC9GbGF0ZURlY29kZQovVHlwZSAvT2JqU3RtCi9OIDUKL0ZpcnN0IDI2Ci9MZW5ndGggMzcyCj4+CnN0cmVhbQp4nNVSTUvD" +
        "QBC976+Yox5kJ5t0N5FS6FcUpCitoCge0mQpkbIryVbqv3cmaS09iGcPj83MvNl9k3kRIChIEojBpJDAIFYwABNnMBwK+fj1YUE+" +
        "FBvbCnlXVy28EgdhCW9CTv3OBYjEaCRO3GkRiq3fiL4JIiYfGQ+Nr3albWCYz/Mc0SCiTggaUc3onBIygqKYaiqlb4JJDqCciRHj" +
        "MdXyHtr0PVzvuIND/5xO4mrmzHpukvbxz7v81ry/Q/2lJxsJufDVrAgWLmbXCpVGE2mMVaqSl0v6HY0tgv+/w3X6a+9+nfBsz7xe" +
        "XnJj2QPdluXStn7XlLR25uWeKvxxa7efNtRlcWUwS0mnSTPyWNdyqmUmUTpVA50eavScfL5fv9uyu4bD+T7crALr6xOcW9iqLiZ+" +
        "T85E9jL2IH+OnfOBHdt51QVSypE++PdsHBYr5Gq3Dl3IyUjISdHaboyTThLhSl/VbgPyqXZj19bHBN/4DbEXzRQKZW5kc3RyZWFt" +
        "CmVuZG9iagoKOCAwIG9iago8PAovU2l6ZSA5Ci9Sb290IDIgMCBSCi9JbmZvIDMgMCBSCi9GaWx0ZXIgL0ZsYXRlRGVjb2RlCi9U" +
        "eXBlIC9YUmVmCi9MZW5ndGggNDIKL1cgWyAxIDIgMiBdCi9JbmRleCBbIDAgOSBdCj4+CnN0cmVhbQp4nGNgYPj/n4mBnYEBRDCC" +
        "CCYQwQwiWBgZBBgYGBkuAQmmNQwMAGI2A8kKZW5kc3RyZWFtCmVuZG9iagoKc3RhcnR4cmVmCjY4NAolJUVPRg==";

    private static PdfPigWordReader Sut() => new(NullLogger<PdfPigWordReader>.Instance);

    [Fact]
    public void ReadWords_NormalizesToTopLeftOrigin()
    {
        var words = Sut().ReadWords(Convert.FromBase64String(FixturePdfBase64));

        var alpha = words.FirstOrDefault(w => w.Text.Contains("Alpha", StringComparison.Ordinal));
        var omega = words.FirstOrDefault(w => w.Text.Contains("Omega", StringComparison.Ordinal));
        alpha.Should().NotBeNull();
        omega.Should().NotBeNull();

        alpha!.PageNumber.Should().Be(1);
        // "Alpha" was drawn near the TOP -> small Y once flipped to top-left origin. A reversed flip fails here.
        alpha.Y.Should().BeLessThan(0.30);
        // "Omega" near the BOTTOM -> large Y.
        omega!.Y.Should().BeGreaterThan(0.60);
        // "Alpha" (x=10) is left of "Omega" (x=120), and every component stays inside the normalized page.
        alpha.X.Should().BeLessThan(omega.X);
        foreach (var w in words)
        {
            w.X.Should().BeInRange(0, 1);
            w.Y.Should().BeInRange(0, 1);
            w.Width.Should().BeInRange(0, 1);
            w.Height.Should().BeInRange(0, 1);
        }
    }

    [Fact]
    public void ReadWords_BytesThatAreNotAPdf_ReturnsEmpty()
    {
        Sut().ReadWords(new byte[] { 1, 2, 3 }).Should().BeEmpty();
    }
}
