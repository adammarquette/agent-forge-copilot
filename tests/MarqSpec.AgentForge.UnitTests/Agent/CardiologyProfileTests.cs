using FluentAssertions;
using MarqSpec.AgentForge.Agent;

namespace MarqSpec.AgentForge.UnitTests.Agent;

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

    [Fact]
    public void SystemPrompt_Always_DirectsTheBriefToSurfaceIngestedDocumentFacts()
    {
        // The brief must surface findings that are only in an uploaded document, not just the structured
        // record - so the prompt must keep directing get_document_facts. reference: gitlab#123.
        CardiologyProfile.SystemPrompt.Should().Contain("get_document_facts");
    }

    [Fact]
    public void SystemPrompt_Always_TellsTheModelNotToNarrateDocumentProvenance()
    {
        // The [Document/<id>] citation carries the source, so a document-derived value is stated and cited,
        // not described as "on the uploaded/outside report" in prose (demo feedback). reference: gitlab#132.
        var normalized = string.Join(' ', CardiologyProfile.SystemPrompt.ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        normalized.Should().Contain("do not narrate its provenance");
    }
}
