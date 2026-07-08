namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// The mandatory verification gate every response passes through before reaching the clinician
/// (FR-VERIF-0) - the single entry point combining source attribution (FR-VERIF-1) and cardiology
/// domain-constraint checks (FR-VERIF-2).
/// </summary>
public interface IClinicalResponseVerifier
{
    /// <summary>
    /// Verifies <paramref name="answer"/> against every tool result returned this turn
    /// (<paramref name="toolResultJson"/>), returning what actually ships.
    /// </summary>
    VerificationResult Verify(string answer, IReadOnlyCollection<string> toolResultJson);
}
