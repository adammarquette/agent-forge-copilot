using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Verification;
using GauntletAI.AgentForge.Verification.Rules;

namespace GauntletAI.AgentForge.UnitTests.Verification.Rules;

public sealed class InrTherapeuticRangeRuleTests
{
    private readonly InrTherapeuticRangeRule _sut = new();

    private static ObservationRecord InrLab(double value) => new(
        new ClinicalSourceRef("Observation", "1"), "laboratory", "INR", value, null, 0.8, 1.2, null, "final");

    private static ConditionRecord Problem(string display) => new(
        new ClinicalSourceRef("Condition", "1"), display, "active", null);

    [Fact]
    public void Evaluate_InrBelowAfibRangeWithAfibOnFile_FlagsViolation()
    {
        var input = new DomainConstraintInput([], [InrLab(1.5)], [Problem("Atrial fibrillation")]);

        var flags = _sut.Evaluate(input);

        flags.Should().ContainSingle();
        flags[0].RuleId.Should().Be("inr-therapeutic-range");
        flags[0].Sources.Should().ContainSingle().Which.Citation.Should().Be("Observation/1");
    }

    [Fact]
    public void Evaluate_InrWithinAfibRange_DoesNotFlag()
    {
        var input = new DomainConstraintInput([], [InrLab(2.5)], [Problem("Atrial fibrillation")]);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_InrBelowMechanicalValveRangeWithValveOnFile_FlagsUsingTheHigherValveRange()
    {
        // 2.2 is within the AFib range (2.0-3.0) but below the mechanical-valve range
        // (2.5-3.5) - proves indication detection actually switches which range applies,
        // not just a single hardcoded band.
        var input = new DomainConstraintInput([], [InrLab(2.2)], [Problem("Mechanical mitral valve")]);

        var flags = _sut.Evaluate(input);

        flags.Should().ContainSingle();
        flags[0].Description.Should().Contain("mechanical valve");
    }

    [Fact]
    public void Evaluate_InrWithinMechanicalValveRangeWithValveOnFile_DoesNotFlag()
    {
        var input = new DomainConstraintInput([], [InrLab(3.2)], [Problem("Mechanical aortic valve")]);

        // Sanity check paired with the test above: 3.2 is in-range for a valve (2.5-3.5).
        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_NoInrLabPresent_ReturnsNoFlags()
    {
        var potassium = new ObservationRecord(new ClinicalSourceRef("Observation", "2"), "laboratory", "Potassium", 4.0, "mEq/L", 3.5, 5.0, null, "final");
        var input = new DomainConstraintInput([], [potassium], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_InrOutOfRangeWithNoIndicationOnFile_FlagsUsingTheDefaultAfibRange()
    {
        // Illustrative default (documented on the rule): AFib is the most common anticoagulation
        // indication for this product's user (USERS.md), used when no indication is on file -
        // not a substitute for a clinician confirming the actual indication.
        var input = new DomainConstraintInput([], [InrLab(4.0)], []);

        var flags = _sut.Evaluate(input);

        flags.Should().ContainSingle();
        flags[0].Description.Should().Contain("atrial fibrillation");
    }
}
