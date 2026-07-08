using FluentAssertions;
using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.UnitTests.Verification;

public sealed class SourceAttributionEngineTests
{
    private readonly SourceAttributionEngine _sut = new();

    [Fact]
    public void Verify_LineWithResolvableCitation_KeepsTheLineAndPasses()
    {
        const string answer = "Warfarin 5mg daily [MedicationRequest/456].";

        var result = _sut.Verify(answer, ["MedicationRequest/456"]);

        result.Passed.Should().BeTrue();
        result.VerifiedAnswer.Should().Be(answer);
        result.SuppressedClaims.Should().BeEmpty();
    }

    [Fact]
    public void Verify_LineWithUnresolvableCitation_SuppressesTheLineAndFails()
    {
        const string answer = "Warfarin 5mg daily [MedicationRequest/999].";

        var result = _sut.Verify(answer, ["MedicationRequest/456"]);

        result.Passed.Should().BeFalse();
        result.VerifiedAnswer.Should().BeEmpty();
        result.SuppressedClaims.Should().ContainSingle().Which.Reason.Should().Contain("MedicationRequest/999");
    }

    [Fact]
    public void Verify_LineWithClinicalKeywordAndNoCitation_SuppressesTheLineAndFails()
    {
        const string answer = "Her INR is 3.5, well above the therapeutic range.";

        var result = _sut.Verify(answer, []);

        result.Passed.Should().BeFalse();
        result.VerifiedAnswer.Should().BeEmpty();
        result.SuppressedClaims.Should().ContainSingle().Which.Line.Should().Be(answer);
    }

    [Fact]
    public void Verify_LineReportingAGapWithNoCitation_KeepsTheLineAndPasses()
    {
        // Matches CardiologyProfile's own gap-reporting example - a line naming a clinical
        // concept (INR) with no value to cite, because there is nothing on file, must not be
        // suppressed the same way a fabricated/uncited claim would be (UC-5).
        const string answer = "No INR on file since 2025-11, cannot verify therapeutic range.";

        var result = _sut.Verify(answer, []);

        result.Passed.Should().BeTrue();
        result.VerifiedAnswer.Should().Be(answer);
        result.SuppressedClaims.Should().BeEmpty();
    }

    [Fact]
    public void Verify_ProseLineWithNoClinicalKeywordsAndNoCitation_KeepsTheLine()
    {
        const string answer = "Please have her follow up again in three months.";

        var result = _sut.Verify(answer, []);

        result.Passed.Should().BeTrue();
        result.VerifiedAnswer.Should().Be(answer);
    }

    [Fact]
    public void Verify_MultipleLinesOneOffending_OnlySuppressesTheOffendingLine()
    {
        const string firstLine = "Active problems: atrial fibrillation [Condition/1].";
        const string secondLine = "Her potassium is 6.2, markedly elevated.";
        var answer = $"{firstLine}\n{secondLine}";

        var result = _sut.Verify(answer, ["Condition/1"]);

        result.Passed.Should().BeFalse();
        result.VerifiedAnswer.Should().Be(firstLine);
        result.SuppressedClaims.Should().ContainSingle().Which.Line.Should().Be(secondLine);
    }

    [Fact]
    public void Verify_UncitedClinicalValueOnALineThatHappensToContainTheWordNo_StillSuppressesIt()
    {
        // Regression: the bare "no " gap-indicator phrase must not swallow a real, uncited
        // clinical claim just because "no" appears somewhere earlier in the sentence (e.g. as
        // part of "no significant"). Only a line that is *actually reporting an absence* should
        // be exempted - this line asserts a specific, elevated potassium value with no citation.
        const string answer = "No significant change, but her potassium is 6.2 today.";

        var result = _sut.Verify(answer, []);

        result.Passed.Should().BeFalse();
        result.SuppressedClaims.Should().ContainSingle().Which.Line.Should().Be(answer);
    }

    [Fact]
    public void Verify_EmptyAnswer_ReturnsPassedTrueWithEmptyAnswer()
    {
        var result = _sut.Verify(string.Empty, []);

        result.Passed.Should().BeTrue();
        result.VerifiedAnswer.Should().BeEmpty();
    }
}
