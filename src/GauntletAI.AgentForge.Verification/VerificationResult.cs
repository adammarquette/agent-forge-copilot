namespace GauntletAI.AgentForge.Verification;

/// <summary>The outcome of running a draft answer through the mandatory verification gate (FR-VERIF-0).</summary>
/// <param name="Passed">
/// <see langword="true"/> only when nothing was suppressed - domain-constraint flags do not
/// affect this: a flagged-but-cited response still ships (with the flag surfaced), since a
/// safety flag is information for the clinician, not evidence the answer is untrustworthy.
/// </param>
/// <param name="VerifiedAnswer">What actually ships - the draft answer with every suppressed line removed.</param>
/// <param name="SuppressedClaims">Every line removed for failing source attribution, and why.</param>
/// <param name="ConstraintFlags">Every cardiology domain-constraint violation found, to surface alongside the answer.</param>
public sealed record VerificationResult(
    bool Passed,
    string VerifiedAnswer,
    IReadOnlyList<SuppressedClaim> SuppressedClaims,
    IReadOnlyList<DomainConstraintFlag> ConstraintFlags);
