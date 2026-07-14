namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <summary>Outcome of a source-document write. OpenEMR's standard document POST returns only a success
/// boolean (no id), so this result carries status only — the citation id is resolved separately via a
/// DocumentReference lookup. reference: agent-forge#43</summary>
public enum DocumentWriteStatus
{
    /// <summary>The document was accepted (2xx).</summary>
    Written,

    /// <summary>Rejected for auth reasons (401/403, or no admin token) — a scope/ACL gap. Degrade, never fabricate.</summary>
    Unauthorized,

    /// <summary>The write failed after resilience (5xx, transport error, timeout).</summary>
    Failed,
}

/// <summary>The result of an <see cref="IOpenEmrDocumentWriter"/> call. Never carries PHI.</summary>
public sealed record DocumentWriteResult(DocumentWriteStatus Status, string? Detail = null)
{
    /// <summary>True only when the document was written.</summary>
    public bool Succeeded => Status == DocumentWriteStatus.Written;

    /// <summary>The document was accepted.</summary>
    public static DocumentWriteResult Written() => new(DocumentWriteStatus.Written);

    /// <summary>Auth failure — scope/ACL gap or missing admin token.</summary>
    public static DocumentWriteResult Unauthorized(string detail) => new(DocumentWriteStatus.Unauthorized, detail);

    /// <summary>A non-auth failure after resilience.</summary>
    public static DocumentWriteResult Failed(string detail) => new(DocumentWriteStatus.Failed, detail);
}
