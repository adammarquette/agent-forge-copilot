using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

namespace GauntletAI.AgentForge.Mcp;

/// <summary>
/// Result of the <c>get_interval_changes</c> tool.
/// </summary>
/// <param name="MedicationChanges">
/// Medication requests authored on or after the requested since-date. Scope note: OpenEMR/FHIR
/// exposes no "status changed at" timestamp distinct from authoredOn, so a pure stop with no new
/// authored request isn't detectable this way - this surfaces what the data actually supports
/// (new starts and re-authored changes), not a fabricated complete start/stop/change diff.
/// </param>
/// <param name="NewLabs">Lab Observations since the since-date (with reference ranges for the caller to assess).</param>
/// <param name="IntervalEncounters">Encounters since the since-date.</param>
public sealed record IntervalChangesResult(
    IReadOnlyList<MedicationRecord> MedicationChanges,
    IReadOnlyList<ObservationRecord> NewLabs,
    IReadOnlyList<EncounterRecord> IntervalEncounters);
