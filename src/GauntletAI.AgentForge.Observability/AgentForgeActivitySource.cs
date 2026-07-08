using System.Diagnostics;

namespace GauntletAI.AgentForge.Observability;

/// <summary>
/// The single <see cref="ActivitySource"/> every layer of the pipeline uses to emit spans - step
/// order and per-step latency (FR-OBS-2). A plain static field, not interface-wrapped like
/// <see cref="IAgentForgeMetrics"/>: starting an <see cref="Activity"/> is a no-op (returns
/// <see langword="null"/>) when nothing is listening - no <c>TracerProvider</c> registered, as in a
/// unit test - so there is no external dependency here to fake.
/// </summary>
public static class AgentForgeActivitySource
{
    /// <summary>Name every OTel <c>TracerProvider</c> registration must include (Program.cs).</summary>
    public const string Name = "GauntletAI.AgentForge";

    /// <summary>The shared source every span in the pipeline starts from.</summary>
    public static readonly ActivitySource Instance = new(Name);
}
