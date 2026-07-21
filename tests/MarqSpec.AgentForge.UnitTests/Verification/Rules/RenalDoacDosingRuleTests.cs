using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.Verification;
using MarqSpec.AgentForge.Verification.Rules;

namespace MarqSpec.AgentForge.UnitTests.Verification.Rules;

public sealed class RenalDoacDosingRuleTests
{
    private readonly RenalDoacDosingRule _sut = new();

    private static MedicationRecord ActiveMed(string display) => new(
        new ClinicalSourceRef("MedicationRequest", "1"), display, null, "active", null);

    private static ObservationRecord CreatinineLab(double value) => new(
        new ClinicalSourceRef("Observation", "1"), "laboratory", "Creatinine", value, "mg/dL", 0.6, 1.2, null, "final");

    [Fact]
    public void Evaluate_DoacWithElevatedCreatinine_FlagsForRenalDoseReview()
    {
        var input = new DomainConstraintInput([ActiveMed("Apixaban 5mg")], [CreatinineLab(1.8)], []);

        var flags = _sut.Evaluate(input);

        flags.Should().ContainSingle();
        flags[0].RuleId.Should().Be("renal-doac-dosing");
        flags[0].Sources.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_DoacWithNormalCreatinine_DoesNotFlag()
    {
        var input = new DomainConstraintInput([ActiveMed("Apixaban 5mg")], [CreatinineLab(0.9)], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ElevatedCreatinineWithNoDoac_DoesNotFlag()
    {
        var input = new DomainConstraintInput([ActiveMed("Metoprolol 50mg")], [CreatinineLab(1.8)], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WarfarinNotADoacWithElevatedCreatinine_DoesNotFlag()
    {
        // Warfarin dosing isn't renally cleared the same way DOACs are - the rule must not
        // over-fire on every anticoagulant.
        var input = new DomainConstraintInput([ActiveMed("Warfarin 5mg")], [CreatinineLab(1.8)], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }
}
