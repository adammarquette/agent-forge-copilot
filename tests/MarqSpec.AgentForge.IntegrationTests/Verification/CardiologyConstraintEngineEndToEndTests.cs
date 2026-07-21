using FluentAssertions;
using MarqSpec.AgentForge.Mcp;
using MarqSpec.AgentForge.Verification;

namespace MarqSpec.AgentForge.IntegrationTests.Verification;

/// <summary>
/// Exercises <see cref="CardiologyConstraintEngine"/>'s rules against a real lab's real
/// <c>CodeDisplay</c> text from the QA OpenEMR deployment - the one thing a unit test's hand-typed
/// "INR" fixture can't prove: that the rule's keyword match actually recognizes how this specific
/// FHIR deployment labels the observation, not just how we assumed it would.
/// </summary>
[Trait("Metric", "M2-ConstraintRecall")]
public sealed class CardiologyConstraintEngineEndToEndTests : IClassFixture<VerificationQaFixture>
{
    private readonly VerificationQaFixture _fixture;

    public CardiologyConstraintEngineEndToEndTests(VerificationQaFixture fixture) => _fixture = fixture;

    [Fact(Skip =
        "QA test patient (Ada Testpatient) has no INR lab Observation on file - issue #26. The test's whole " +
        "point is proving the rule matches this deployment's real CodeDisplay text, so there's no meaningful " +
        "way to relax the assertion; it needs an actual INR lab seeded into QA OpenEMR to mean anything.")]
    public async Task Evaluate_RealInrLabForcedOutOfRange_FiresUsingTheLabsOwnRealCodeDisplayAndSource()
    {
        // The "seeded violation" here is forcing the *value* of a real, fetched lab out of range
        // while keeping every other field (Source, CodeDisplay) exactly as OpenEMR returned it -
        // proving the rule's "CodeDisplay contains INR" match works against this deployment's
        // actual field text, not a hand-typed unit-test fixture. Writing a real out-of-range
        // Observation into QA OpenEMR isn't possible under this product's read-only FHIR scopes
        // (INTERFACE_CONTROL.md A.4), so this is the closest real equivalent.
        var labs = await _fixture.ToolServer.GetLabsAsync(
            new GetLabsRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.OpenEmr.Options.TestPatientId! },
            CancellationToken.None);
        var inrLab = labs.Labs.FirstOrDefault(l => l.CodeDisplay.Contains("INR", StringComparison.OrdinalIgnoreCase));
        inrLab.Should().NotBeNull(
            "the QA test patient should carry at least one INR lab (INTERFACE_CONTROL.md's cardiology lab " +
            "set) to prove this rule's keyword match against real FHIR CodeDisplay text");

        var forcedOutOfRange = inrLab! with { Value = 9.9 };

        var flags = _fixture.ConstraintEngine.Evaluate(new DomainConstraintInput([], [forcedOutOfRange], []));

        flags.Should().Contain(f => f.RuleId == "inr-therapeutic-range" && f.Sources.Contains(inrLab.Source));
    }
}
