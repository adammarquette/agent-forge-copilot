using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.UnitTests.Verification;

public sealed class CardiologyConstraintEngineTests
{
    [Fact]
    public void Evaluate_MultipleRulesEachFlag_ReturnsFlagsFromEveryRule()
    {
        var ruleA = A.Fake<IDomainConstraintRule>();
        var ruleB = A.Fake<IDomainConstraintRule>();
        var flagA = new DomainConstraintFlag("rule-a", "flag from a", [new ClinicalSourceRef("Observation", "1")]);
        var flagB = new DomainConstraintFlag("rule-b", "flag from b", [new ClinicalSourceRef("MedicationRequest", "2")]);
        var input = DomainConstraintInput.Empty;
        A.CallTo(() => ruleA.Evaluate(input)).Returns([flagA]);
        A.CallTo(() => ruleB.Evaluate(input)).Returns([flagB]);
        var sut = new CardiologyConstraintEngine([ruleA, ruleB]);

        var flags = sut.Evaluate(input);

        flags.Should().BeEquivalentTo([flagA, flagB]);
    }

    [Fact]
    public void Evaluate_NoRuleFlags_ReturnsEmpty()
    {
        var rule = A.Fake<IDomainConstraintRule>();
        var input = DomainConstraintInput.Empty;
        A.CallTo(() => rule.Evaluate(input)).Returns([]);
        var sut = new CardiologyConstraintEngine([rule]);

        sut.Evaluate(input).Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_OneRuleThrows_TheOtherRulesStillRunAndTheThrowingRuleIsSkipped()
    {
        // One rule's bug (e.g. a null-ref on unexpected data) must degrade one check, not take
        // down verification for the whole response (NFR-REL-1's "one failure degrades one
        // section" applied to the rule engine itself).
        var badRule = A.Fake<IDomainConstraintRule>();
        var goodRule = A.Fake<IDomainConstraintRule>();
        var flag = new DomainConstraintFlag("good-rule", "flag", []);
        var input = DomainConstraintInput.Empty;
        A.CallTo(() => badRule.Evaluate(input)).Throws<InvalidOperationException>();
        A.CallTo(() => goodRule.Evaluate(input)).Returns([flag]);
        var sut = new CardiologyConstraintEngine([badRule, goodRule]);

        var flags = sut.Evaluate(input);

        flags.Should().ContainSingle().Which.Should().Be(flag);
    }
}
