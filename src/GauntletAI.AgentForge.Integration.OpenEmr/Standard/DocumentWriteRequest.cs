namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <summary>
/// Inputs for a single source-document write to OpenEMR via the standard REST API. The document is written
/// under an <b>administrative identity</b> ahead of the visit (W2_ARCHITECTURE.md §4 / §11.3), not the
/// clinician's token. reference: documentation/W2_ARCHITECTURE.md §4
/// </summary>
public sealed record DocumentWriteRequest
{
    /// <summary>OpenEMR patient id (pid) the document belongs to.</summary>
    public required string PatientId { get; init; }

    /// <summary>Original file name stored on the OpenEMR document.</summary>
    public required string FileName { get; init; }

    /// <summary>Raw file bytes.</summary>
    public required byte[] Content { get; init; }

    /// <summary>IANA media type of the file (e.g. <c>application/pdf</c>).</summary>
    public required string MediaType { get; init; }

    /// <summary>OpenEMR document category path to file the document under (the ingestion category).</summary>
    public required string CategoryPath { get; init; }

    /// <summary>Optional encounter id (<c>eid</c>) to associate the document with.</summary>
    public string? EncounterId { get; init; }
}
