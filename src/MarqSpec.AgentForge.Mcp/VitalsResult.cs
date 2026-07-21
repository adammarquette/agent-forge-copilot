using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.Mcp;

/// <summary>Result of the <c>get_vitals</c> tool.</summary>
/// <param name="Vitals">Vital-signs Observations, each with its value, unit, and citation.</param>
public sealed record VitalsResult(IReadOnlyList<ObservationRecord> Vitals);
