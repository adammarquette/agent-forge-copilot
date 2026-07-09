using System.Text.Json;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Verification;

public sealed class ClinicalResponseVerifierTests
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ClinicalResponseVerifier _sut = new(
        new SourceAttributionEngine(),
        new CardiologyConstraintEngine(CardiologyConstraintRules.Default),
        NullLogger<ClinicalResponseVerifier>.Instance);

    [Fact]
    public void Verify_AnswerWithResolvableCitation_PassesWithNoSuppressions()
    {
        var medJson = JsonSerializer.Serialize(
            new PatientSummaryResult(null, [], [new MedicationRecord(new ClinicalSourceRef("MedicationRequest", "456"), "Warfarin 5mg", null, "active", null)], []),
            SerializeOptions);

        var result = _sut.Verify("Warfarin 5mg daily [MedicationRequest/456].", [medJson]);

        result.Passed.Should().BeTrue();
        result.VerifiedAnswer.Should().Be("Warfarin 5mg daily [MedicationRequest/456].");
        result.SuppressedClaims.Should().BeEmpty();
    }

    [Fact]
    public void Verify_AnswerWithUnresolvableCitation_FailsAndSuppressesTheLine()
    {
        var result = _sut.Verify("Warfarin 5mg daily [MedicationRequest/999].", []);

        result.Passed.Should().BeFalse();
        result.VerifiedAnswer.Should().BeEmpty();
        result.SuppressedClaims.Should().ContainSingle();
    }

    [Fact]
    public void Verify_ToolResultTriggersADomainConstraintRule_ReturnsTheFlag()
    {
        var labJson = JsonSerializer.Serialize(
            new LabsResult([new ObservationRecord(new ClinicalSourceRef("Observation", "1"), "laboratory", "INR", 5.0, null, 0.8, 1.2, null, "final")]),
            SerializeOptions);
        var problemJson = JsonSerializer.Serialize(
            new PatientSummaryResult(null, [new ConditionRecord(new ClinicalSourceRef("Condition", "1"), "Atrial fibrillation", "active", null)], [], []),
            SerializeOptions);

        var result = _sut.Verify("No new concerns today.", [labJson, problemJson]);

        result.ConstraintFlags.Should().ContainSingle().Which.RuleId.Should().Be("inr-therapeutic-range");
    }

    [Fact]
    public void Verify_NoToolResults_StillEvaluatesAttributionAgainstAnEmptyCitationSet()
    {
        var result = _sut.Verify("Please follow up in three months.", []);

        result.Passed.Should().BeTrue();
        result.ConstraintFlags.Should().BeEmpty();
    }
}
