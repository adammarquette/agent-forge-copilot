namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <summary>
/// Writes a source document to OpenEMR under the administrative identity, with deterministic degradation:
/// it never throws for an OpenEMR-side failure and never fabricates success (ARCHITECTURE.md §13.1). Auth
/// failures map to <see cref="DocumentWriteStatus.Unauthorized"/> so the caller can surface a scope/ACL gap
/// rather than retrying blindly.
/// </summary>
public interface IOpenEmrDocumentWriter
{
    /// <summary>Writes the document; see <see cref="DocumentWriteResult"/> for outcomes.</summary>
    Task<DocumentWriteResult> WriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken = default);
}
