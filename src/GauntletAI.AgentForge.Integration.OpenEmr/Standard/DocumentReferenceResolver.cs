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
    public async Task<IReadOnlySet<string>> SnapshotAsync(
        string patientId, CancellationToken cancellationToken = default)
    {
        // A snapshot is a precondition for a reliable diff, so failures propagate to the caller rather than
        // degrading to an empty set (which could mis-resolve a later diff).
        var records = await _fhir.GetDocumentReferencesAsync(_site, patientId, cancellationToken).ConfigureAwait(false);
        return records.Select(r => r.Source.Id).ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<ClinicalSourceRef?> ResolveNewAsync(
        string patientId, IReadOnlySet<string> knownBefore, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClinicalDocumentRecord> records;
        try
        {
            records = await _fhir.GetDocumentReferencesAsync(_site, patientId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DocumentReferenceResolverLog.LookupFailed(_logger, ex);
            return null;
        }

        var appeared = records.Where(r => !knownBefore.Contains(r.Source.Id)).ToArray();
        if (appeared.Length == 1)
        {
            return appeared[0].Source;
        }

        if (appeared.Length == 0)
        {
            DocumentReferenceResolverLog.NoNewReference(_logger);
        }
        else
        {
            DocumentReferenceResolverLog.AmbiguousReferences(_logger, appeared.Length);
        }

        return null;
    }
}
