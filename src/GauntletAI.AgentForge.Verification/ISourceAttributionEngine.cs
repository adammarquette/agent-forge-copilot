namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// Resolves every citation the model's draft answer makes against the resources actually
/// returned by tools this turn, and suppresses any line that either cites something that was
/// never returned or asserts a clinical value with no citation at all (FR-VERIF-1).
/// </summary>
public interface ISourceAttributionEngine
{
    /// <summary>
    /// Verifies <paramref name="answer"/> against <paramref name="availableCitations"/> (every
    /// <c>{ResourceType}/{Id}</c> key actually returned by a tool this turn).
    /// </summary>
    AttributionResult Verify(string answer, IReadOnlyCollection<string> availableCitations);
}
