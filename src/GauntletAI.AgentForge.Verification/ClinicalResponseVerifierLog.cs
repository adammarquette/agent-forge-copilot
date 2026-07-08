using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// Source-generated log messages for <see cref="ClinicalResponseVerifier"/> (CA1848) - the
/// pass/fail + reason record FR-VERIF-3 requires. Counts only, never the suppressed text or a
/// clinical value (ENGINEERING_STANDARDS.md §7).
/// </summary>
internal static partial class ClinicalResponseVerifierLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Verification {Outcome}: {SuppressedCount} claim(s) suppressed, {ConstraintFlagCount} domain-constraint flag(s)")]
    public static partial void VerificationCompleted(ILogger logger, string outcome, int suppressedCount, int constraintFlagCount);
}
