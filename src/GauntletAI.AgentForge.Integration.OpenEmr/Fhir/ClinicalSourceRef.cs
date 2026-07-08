namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// Identifies the specific FHIR resource a clinical fact was extracted from. The
/// <see cref="Citation"/> is the key the verification layer resolves every asserted claim
/// against (FR-VERIF-1, INTERFACE_CONTROL.md §B.4) - a claim with no <see cref="ClinicalSourceRef"/>
/// must not be stated as fact.
/// </summary>
/// <param name="ResourceType">FHIR resource type, e.g. <c>MedicationRequest</c>.</param>
/// <param name="Id">The resource's id within that type.</param>
public sealed record ClinicalSourceRef(string ResourceType, string Id)
{
    /// <summary>The <c>{resourceType}/{id}</c> citation key.</summary>
    public string Citation => $"{ResourceType}/{Id}";
}
