using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.Verification;
using MarqSpec.AgentForge.Verification.Rules;

namespace MarqSpec.AgentForge.UnitTests.Verification.Rules;

public sealed class AceiArbHyperkalemiaRuleTests
{
    private readonly AceiArbHyperkalemiaRule _sut = new();

    private static MedicationRecord ActiveMed(string display) => new(
        new ClinicalSourceRef("MedicationRequest", "1"), display, null, "active", null);

    private static ObservationRecord PotassiumLab(double value) => new(
        new ClinicalSourceRef("Observation", "1"), "laboratory", "Potassium", value, "mEq/L", 3.5, 5.0, null, "final");

    [Fact]
    public void Evaluate_AceiWithElevatedPotassium_FlagsViolation()
    {
        var input = new DomainConstraintInput([ActiveMed("Lisinopril 10mg")], [PotassiumLab(5.8)], []);

        var flags = _sut.Evaluate(input);

        flags.Should().ContainSingle();
        flags[0].RuleId.Should().Be("acei-arb-hyperkalemia");
        flags[0].Sources.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_ArbWithElevatedPotassium_FlagsViolation()
    {
        var input = new DomainConstraintInput([ActiveMed("Losartan 50mg")], [PotassiumLab(5.6)], []);

        _sut.Evaluate(input).Should().ContainSingle();
    }

    [Fact]
    public void Evaluate_AceiWithNormalPotassium_DoesNotFlag()
    {
        var input = new DomainConstraintInput([ActiveMed("Lisinopril 10mg")], [PotassiumLab(4.2)], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ElevatedPotassiumWithNoAceiOrArb_DoesNotFlag()
    {
        var input = new DomainConstraintInput([ActiveMed("Metoprolol 50mg")], [PotassiumLab(5.8)], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }
}
