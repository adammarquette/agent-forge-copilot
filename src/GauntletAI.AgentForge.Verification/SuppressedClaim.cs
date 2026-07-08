namespace GauntletAI.AgentForge.Verification;

/// <summary>A line of the model's draft answer that failed source attribution and was removed before shipping.</summary>
/// <param name="Line">The original line, verbatim.</param>
/// <param name="Reason">Why it was suppressed - an unresolvable citation, or a clinical claim with none at all.</param>
public sealed record SuppressedClaim(string Line, string Reason);
