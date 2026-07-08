using FluentAssertions;
using GauntletAI.AgentForge.Agent;

namespace GauntletAI.AgentForge.UnitTests.Agent;

public sealed class CardiologyProfileTests
{
    [Fact]
    public void SystemPrompt_Always_IsNotEmpty()
    {
        CardiologyProfile.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("cite", "grounding discipline (FR-VERIF-1) must survive any future prompt edit")]
    [InlineData("do not diagnose", "NG1 - the co-pilot surfaces and cites, it does not diagnose or recommend treatment")]
    [InlineData("do not recommend", "NG1 - no treatment recommendations")]
    [InlineData("other patient", "FR-CHAT-3 - must refuse to pull a different patient's data into the conversation")]
    [InlineData("cannot verify", "UC-5 - must communicate uncertainty rather than guess when data is missing")]
    public void SystemPrompt_Always_ContainsRequiredSafetyInstruction(string requiredPhrase, string becauseReason)
    {
        // Whitespace-normalized so line-wrapping in the source raw string literal can't spuriously
        // split a required phrase across lines and fail a check that should only care about content.
        var normalized = string.Join(' ', CardiologyProfile.SystemPrompt.ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        normalized.Should().Contain(requiredPhrase, becauseReason);
    }
}
