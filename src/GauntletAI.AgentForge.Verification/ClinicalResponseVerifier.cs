using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// The mandatory gate (FR-VERIF-0): scans this turn's tool results once, runs source attribution
/// (FR-VERIF-1) and the cardiology domain-constraint rules (FR-VERIF-2) against that single scan,
/// and records the pass/fail outcome (FR-VERIF-3).
/// </summary>
public sealed class ClinicalResponseVerifier(
    ISourceAttributionEngine attributionEngine,
    CardiologyConstraintEngine constraintEngine,
    ILogger<ClinicalResponseVerifier> logger) : IClinicalResponseVerifier
{
    /// <inheritdoc />
    public VerificationResult Verify(string answer, IReadOnlyCollection<string> toolResultJson)
    {
        var scan = ToolResultJsonScanner.Scan(toolResultJson);
        var attribution = attributionEngine.Verify(answer, scan.Citations);
        var constraintFlags = constraintEngine.Evaluate(scan.Input);

        ClinicalResponseVerifierLog.VerificationCompleted(
            logger, attribution.Passed ? "passed" : "failed", attribution.SuppressedClaims.Count, constraintFlags.Count);

        return new VerificationResult(attribution.Passed, attribution.VerifiedAnswer, attribution.SuppressedClaims, constraintFlags);
    }
}
