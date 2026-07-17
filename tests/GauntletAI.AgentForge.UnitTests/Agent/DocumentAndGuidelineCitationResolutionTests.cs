using System.Text.Json;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.UnitTests.Agent;

/// <summary>
/// End-to-end grounding guard (gitlab#118): the tool-result shapes get_document_facts and retrieve_evidence
/// emit must resolve through the Week 1 verifier so an answer citing <c>[Document/&lt;slug&gt;]</c> or
/// <c>[Guideline/&lt;chunkId&gt;]</c> is kept (grounded), not suppressed as uncited. Guards against a future
/// change to those record shapes (renaming ResourceType/Id, changing the citation format) silently breaking
/// resolution. Serialized the same way the dispatcher emits them (default System.Text.Json, PascalCase).
/// </summary>
public sealed class DocumentAndGuidelineCitationResolutionTests
{
    private static readonly string DocumentFactsJson = JsonSerializer.Serialize(new DocumentFactsResult(
        [new DocumentFactRecord("Document", "abc12345", "lab.result", "Potassium 5.9 (H) mmol/L", "docref-1", "2")]));

    private static readonly string EvidenceJson = JsonSerializer.Serialize(new EvidenceResult(
        [new EvidenceRecord("Guideline", "chunk-9", "anticoag-2026", "Warfarin INR target", "Target INR 2.0 to 3.0.")]));

    [Fact]
    public void Scan_DocumentAndGuidelineToolResults_RecoversBothAsResolvableCitations()
    {
        var scan = ToolResultJsonScanner.Scan([DocumentFactsJson, EvidenceJson]);

        scan.Citations.Should().Contain("Document/abc12345").And.Contain("Guideline/chunk-9");
    }

    [Fact]
    public void Verify_AnswerCitingDocumentAndGuidelineSourcesThoseToolsReturned_KeepsThoseLines()
    {
        var scan = ToolResultJsonScanner.Scan([DocumentFactsJson, EvidenceJson]);
        var attribution = new SourceAttributionEngine();

        var answer = "Potassium is 5.9 (H) mmol/L [Document/abc12345].\nTarget INR is 2.0 to 3.0 [Guideline/chunk-9].";
        var result = attribution.Verify(answer, scan.Citations);

        result.Passed.Should().BeTrue();
        result.SuppressedClaims.Should().BeEmpty();
    }

    [Fact]
    public void Verify_AnswerCitingADocumentNoToolReturned_SuppressesThatLine()
    {
        var scan = ToolResultJsonScanner.Scan([DocumentFactsJson]);
        var attribution = new SourceAttributionEngine();

        var answer = "Sodium is 148 mmol/L [Document/notreal1].";
        var result = attribution.Verify(answer, scan.Citations);

        result.Passed.Should().BeFalse();
        result.SuppressedClaims.Should().ContainSingle();
    }
}
