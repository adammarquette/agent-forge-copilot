namespace MarqSpec.AgentForge.Api.Chat;

/// <summary>
/// Wire shape for one <c>SuppressedClaim</c> (PRD.md §13.1's "Claim can't be grounded" row -
/// "suppressed items noted" travels with the answer, not just into a log line, the same way
/// <see cref="SafetyFlagPayload"/> does for domain-constraint flags).
/// </summary>
/// <param name="Line">The original draft line that was removed, verbatim.</param>
/// <param name="Reason">Why it was suppressed - an unresolvable citation, or no citation at all.</param>
public sealed record SuppressedClaimPayload(string Line, string Reason);
