namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// A cardiac procedure (PCI, ablation, ...), mapped from a FHIR <c>Procedure</c>
/// (INTERFACE_CONTROL.md §B.1).
/// </summary>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="ProcedureDisplay">Human-readable procedure name.</param>
/// <param name="Status">FHIR <c>Procedure.status</c> (completed, in-progress, ...).</param>
/// <param name="PerformedDate">When the procedure was performed, when present.</param>
public sealed record ProcedureRecord(
    ClinicalSourceRef Source,
    string ProcedureDisplay,
    string Status,
    DateTimeOffset? PerformedDate);
