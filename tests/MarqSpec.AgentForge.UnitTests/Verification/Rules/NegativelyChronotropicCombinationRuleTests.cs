using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.Verification;
using MarqSpec.AgentForge.Verification.Rules;

namespace MarqSpec.AgentForge.UnitTests.Verification.Rules;

public sealed class NegativelyChronotropicCombinationRuleTests
{
    private readonly NegativelyChronotropicCombinationRule _sut = new();

    private static MedicationRecord ActiveMed(string display, string id) => new(
        new ClinicalSourceRef("MedicationRequest", id), display, null, "active", null);

    [Fact]
    public void Evaluate_NonDihydropyridineCcbPlusBetaBlockerBothActive_FlagsTheCombination()
    {
        var input = new DomainConstraintInput(
            [ActiveMed("Diltiazem 120mg", "1"), ActiveMed("Metoprolol 50mg", "2")], [], []);

        var flags = _sut.Evaluate(input);

        flags.Should().ContainSingle();
        flags[0].RuleId.Should().Be("negatively-chronotropic-combination");
        flags[0].Sources.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_DihydropyridineCcbPlusBetaBlocker_DoesNotFlag()
    {
        // Amlodipine is dihydropyridine - not chronotropic - the rule must not over-fire on any CCB.
        var input = new DomainConstraintInput(
            [ActiveMed("Amlodipine 10mg", "1"), ActiveMed("Metoprolol 50mg", "2")], [], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_OnlyBetaBlockerActive_DoesNotFlag()
    {
        var input = new DomainConstraintInput([ActiveMed("Metoprolol 50mg", "2")], [], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_CcbActiveButBetaBlockerStopped_DoesNotFlag()
    {
        var stoppedBetaBlocker = new MedicationRecord(new ClinicalSourceRef("MedicationRequest", "2"), "Metoprolol 50mg", null, "stopped", null);
        var input = new DomainConstraintInput([ActiveMed("Diltiazem 120mg", "1"), stoppedBetaBlocker], [], []);

        _sut.Evaluate(input).Should().BeEmpty();
    }
}
