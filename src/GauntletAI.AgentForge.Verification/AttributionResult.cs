namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// Result of running <see cref="ISourceAttributionEngine"/> over a draft answer (FR-VERIF-1).
/// </summary>
/// <param name="Passed"><see langword="true"/> only when nothing was suppressed.</param>
/// <param name="VerifiedAnswer">The draft answer with every suppressed line removed - what actually ships.</param>
/// <param name="SuppressedClaims">Every line that was removed, and why.</param>
public sealed record AttributionResult(bool Passed, string VerifiedAnswer, IReadOnlyList<SuppressedClaim> SuppressedClaims);
