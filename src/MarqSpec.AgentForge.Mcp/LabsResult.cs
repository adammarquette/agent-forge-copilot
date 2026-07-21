using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.Mcp;

/// <summary>Result of the <c>get_labs</c> tool.</summary>
/// <param name="Labs">Lab Observations, each with its value, unit, reference range, and citation.</param>
public sealed record LabsResult(IReadOnlyList<ObservationRecord> Labs);
