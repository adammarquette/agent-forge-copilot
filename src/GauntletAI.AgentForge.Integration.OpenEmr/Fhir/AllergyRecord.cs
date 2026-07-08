namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// An allergy or intolerance, mapped from a FHIR <c>AllergyIntolerance</c>
/// (INTERFACE_CONTROL.md §B.1).
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="AllergenDisplay">Human-readable allergen name, e.g. "Penicillin".</param>
/// <param name="ClinicalStatus">FHIR <c>clinicalStatus</c> code (active, inactive, resolved, ...).</param>
/// <param name="Criticality">FHIR <c>criticality</c> (low, high, unable-to-assess), when present.</param>
/// <param name="ReactionManifestation">First recorded reaction manifestation, when present.</param>
public sealed record AllergyRecord(
    ClinicalSourceRef Source,
    string AllergenDisplay,
    string ClinicalStatus,
    string? Criticality,
    string? ReactionManifestation);
