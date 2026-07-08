using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// Input for the <c>get_documents</c> tool (ARCHITECTURE.md §8.1) - DiagnosticReport /
/// DocumentReference narrative (echo/EF, device), returned with source ref (FR-DATA-4).
/// </summary>
public sealed record GetDocumentsRequest
{
    /// <summary>OpenEMR multi-site segment.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Site { get; init; }

    /// <summary>The patient this query is scoped to (minimum-necessary - never whole-chart).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string PatientId { get; init; }

    /// <summary>
    /// Case-insensitive substring filter on document type (e.g. "echo"). Applied client-side -
    /// the FHIR type search-param mapping isn't confirmed against the live server
    /// (INTERFACE_CONTROL.md [CONFIRM]). Optional - omitted returns every document type.
    /// </summary>
    public string? DocumentType { get; init; }
}
