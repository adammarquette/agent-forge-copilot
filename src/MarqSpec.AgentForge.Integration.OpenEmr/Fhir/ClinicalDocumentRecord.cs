namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// A cardiology-relevant document (echo report, device interrogation, outside records), mapped
/// from a FHIR <c>DiagnosticReport</c> or <c>DocumentReference</c> (INTERFACE_CONTROL.md §B.1).
/// </summary>
/// <remarks>
/// This is the document-metadata layer only: <see cref="NarrativeText"/> is the report's plain
/// text as OpenEMR recorded it, cited to <see cref="Source"/>. Extracting a specific clinical
/// value from that narrative - e.g. an ejection-fraction percentage from an echo conclusion - is
/// a downstream concern for the agent/LLM layer, not this mapper, and any such extracted value
/// must be labeled "derived" per FR-DATA-4 when it is surfaced. ARCHITECTURE.md §7 flags
/// extraction fidelity as [PROVISIONAL] pending a running instance to validate against.
/// </remarks>
/// <param name="Source">Citation key for source attribution (FR-VERIF-1).</param>
/// <param name="DocumentType">Human-readable document/report type.</param>
/// <param name="Status">FHIR resource status (final, current, ...).</param>
/// <param name="DateTime">When the report/document was effective, when present.</param>
/// <param name="NarrativeText">Plain-text report content, when present and decodable.</param>
public sealed record ClinicalDocumentRecord(
    ClinicalSourceRef Source,
    string DocumentType,
    string? Status,
    DateTimeOffset? DateTime,
    string? NarrativeText);
