using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// Input for the <c>get_patient_summary</c> tool (ARCHITECTURE.md §8.1) - demographics + active
/// problems + active meds + allergies, one bounded bundle for a single patient.
/// </summary>
public sealed record GetPatientSummaryRequest
{
    /// <summary>OpenEMR multi-site segment.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Site { get; init; }

    /// <summary>The patient this bundle is scoped to (minimum-necessary - never whole-chart).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string PatientId { get; init; }
}
