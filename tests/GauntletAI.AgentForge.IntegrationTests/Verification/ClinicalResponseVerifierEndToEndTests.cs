using System.Text.Json;
using FluentAssertions;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.IntegrationTests.Verification;

/// <summary>
/// Exercises the real <see cref="ClinicalResponseVerifier"/> against real tool-result JSON from
/// the QA OpenEMR deployment - the contract-drift coverage a unit test's hand-built JSON fixtures
/// can't provide: does <see cref="ToolResultJsonScanner"/> correctly parse the JSON shape
/// <see cref="McpToolServer"/>'s real results actually serialize to, and do real FHIR field values
/// (ids, CodeDisplay text) actually resolve/match the way the engines expect.
/// </summary>
public sealed class ClinicalResponseVerifierEndToEndTests : IClassFixture<VerificationQaFixture>
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly VerificationQaFixture _fixture;
    private readonly ClinicalResponseVerifier _verifier;

    public ClinicalResponseVerifierEndToEndTests(VerificationQaFixture fixture)
    {
        _fixture = fixture;
        _verifier = new ClinicalResponseVerifier(
            fixture.AttributionEngine, fixture.ConstraintEngine, NullLogger<ClinicalResponseVerifier>.Instance);
    }

    [Fact]
    public async Task Verify_AnswerCitingARealResourceFromARealToolResult_PassesVerification()
    {
        var summary = await _fixture.ToolServer.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.OpenEmr.Options.TestPatientId! },
            CancellationToken.None);
        var (citation, display) = FirstCitedFact(summary);
        var toolResultJson = JsonSerializer.Serialize(summary, SerializeOptions);
        var answer = $"{display} [{citation}].";

        var result = _verifier.Verify(answer, [toolResultJson]);

        result.Passed.Should().BeTrue();
        result.VerifiedAnswer.Should().Be(answer);
        result.SuppressedClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_AnswerWithAFabricatedCitation_IsSuppressedEvenAgainstRealToolResultJson()
    {
        // Guards "an unattributable claim is suppressed, not shown" (this issue's QA bullet)
        // against the real wire shape - a unit test's own hand-built JSON can't prove
        // ToolResultJsonScanner parses the real McpToolServer shape correctly enough to still
        // catch a fabricated citation.
        var summary = await _fixture.ToolServer.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.OpenEmr.Options.TestPatientId! },
            CancellationToken.None);
        var toolResultJson = JsonSerializer.Serialize(summary, SerializeOptions);
        const string answer = "Patient is on lisinopril 10mg [MedicationRequest/definitely-fake-id-999999].";

        var result = _verifier.Verify(answer, [toolResultJson]);

        result.Passed.Should().BeFalse();
        result.VerifiedAnswer.Should().BeEmpty();
        result.SuppressedClaims.Should().ContainSingle().Which.Reason.Should().Contain("MedicationRequest/definitely-fake-id-999999");
    }

    private static (string Citation, string Display) FirstCitedFact(PatientSummaryResult summary)
    {
        if (summary.ActiveMedications.Count > 0)
        {
            return (summary.ActiveMedications[0].Source.Citation, summary.ActiveMedications[0].MedicationDisplay);
        }

        if (summary.ActiveProblems.Count > 0)
        {
            return (summary.ActiveProblems[0].Source.Citation, summary.ActiveProblems[0].ProblemDisplay);
        }

        throw new InvalidOperationException(
            "QA test patient has no active medications or problems on file - need at least one real " +
            "cited fact to exercise source attribution against real data.");
    }
}
