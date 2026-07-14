using Refit;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <summary>
/// The OpenEMR <b>standard</b> REST document endpoint (not FHIR — the FHIR API is read-only for our data
/// types, W2-D3). Source documents are written here. Multipart: the file part is named <c>document</c>
/// (<c>$_FILES['document']</c>); <c>path</c> (category) and <c>eid</c> are query params — matches the fork
/// route handler. reference: agent-forge#43
/// </summary>
public interface IOpenEmrDocumentApi
{
    /// <summary>
    /// POSTs a source document for a patient. Returns the raw HTTP response so the caller maps status to a
    /// <see cref="DocumentWriteResult"/> — the response body is a bare success flag with no id.
    /// </summary>
    [Multipart]
    [Post("/apis/{site}/api/patient/{pid}/document")]
    Task<HttpResponseMessage> UploadDocumentAsync(
        string site,
        string pid,
        [Query][AliasAs("path")] string path,
        [Query][AliasAs("eid")] string? eid,
        [AliasAs("document")] ByteArrayPart document,
        CancellationToken cancellationToken = default);
}
