using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.Mcp;

/// <summary>Result of the <c>get_patient_summary</c> tool.</summary>
/// <param name="Patient">Demographics, or null when the patient wasn't found.</param>
/// <param name="ActiveProblems">The problem list.</param>
/// <param name="ActiveMedications">Current medication requests.</param>
/// <param name="Allergies">Allergies/intolerances.</param>
public sealed record PatientSummaryResult(
    PatientRecord? Patient,
    IReadOnlyList<ConditionRecord> ActiveProblems,
    IReadOnlyList<MedicationRecord> ActiveMedications,
    IReadOnlyList<AllergyRecord> Allergies);
