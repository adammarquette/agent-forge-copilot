using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.Integration.OpenEmr.Standard;

/// <inheritdoc />
public sealed class DocumentReferenceResolver : IDocumentReferenceResolver
{
    private readonly IOpenEmrFhirClient _fhir;
    private readonly string _site;
    private readonly ILogger<DocumentReferenceResolver> _logger;

    /// <summary>Creates the resolver.</summary>
    public DocumentReferenceResolver(
        IOpenEmrFhirClient fhir, IOptions<OpenEmrOptions> options, ILogger<DocumentReferenceResolver> logger)
    {
        _fhir = fhir;
        _site = options.Value.Site;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> SnapshotAsync(string patientId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<ClinicalSourceRef?> ResolveNewAsync(
        string patientId, IReadOnlySet<string> knownBefore, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
