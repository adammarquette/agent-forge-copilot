using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.Mcp;

/// <summary>Result of the <c>get_recent_encounters</c> tool.</summary>
/// <param name="Encounters">The most recent encounters, newest first.</param>
public sealed record RecentEncountersResult(IReadOnlyList<EncounterRecord> Encounters);
