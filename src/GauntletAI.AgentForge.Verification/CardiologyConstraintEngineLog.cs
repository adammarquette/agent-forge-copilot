using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Verification;

/// <summary>Source-generated log messages for <see cref="CardiologyConstraintEngine"/> (CA1848).</summary>
internal static partial class CardiologyConstraintEngineLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Domain constraint rule {RuleId} threw and was skipped for this evaluation")]
    public static partial void RuleThrew(ILogger logger, string ruleId, Exception exception);
}
