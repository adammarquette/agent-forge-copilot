using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.Verification;
using MarqSpec.AgentForge.Verification.Rules;

namespace MarqSpec.AgentForge.UnitTests.Verification.Rules;

public sealed class QtProlongingCombinationRuleTests
{
    private readonly QtProlongingCombinationRule _sut = new();

    private static MedicationRecord ActiveMed(string display, string id) => new(
        new ClinicalSourceRef("MedicationRequest", id), display, null, "active", null);

    [Fact]
    public void Evaluate_TwoActiveQtProlongingMedications_FlagsTheCombination()
    {
        var input = new DomainConstraintInput(
            [ActiveMed("Amiodarone 200mg", "1"), ActiveMed("Sotalol 80mg", "2")], [], []);

        var flags = _sut.Evaluate(input);

        flags.Should().ContainSingle();
        flags[0].RuleId.Should().Be("qt-prolonging-combination");
        flags[0].Sources.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_OnlyOneQtProlongingMedication_DoesNotFlag()
    {
        var input = new DomainConstraintInput([ActiveMed("Amiodarone 200mg", "1")], [], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_SecondQtProlongingMedicationIsNotActive_DoesNotFlag()
    {
        var stoppedMed = new MedicationRecord(new ClinicalSourceRef("MedicationRequest", "2"), "Sotalol 80mg", null, "stopped", null);
        var input = new DomainConstraintInput([ActiveMed("Amiodarone 200mg", "1"), stoppedMed], [], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_NoQtProlongingMedications_ReturnsNoFlags()
    {
        var input = new DomainConstraintInput([ActiveMed("Lisinopril 10mg", "1")], [], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }
}
