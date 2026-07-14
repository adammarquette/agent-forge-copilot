using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Agents;

/// <summary>Source-generated log messages for <see cref="EvidenceAgentSupervisor"/> (CA1848). PHI-free.</summary>
internal static partial class EvidenceAgentSupervisorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Handoff {From} -> {To}: {Reason}")]
    public static partial void Handoff(ILogger logger, string from, string to, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Extraction rejected during evidence-agent run; continuing without document facts")]
    public static partial void ExtractionDegraded(ILogger logger);
}
