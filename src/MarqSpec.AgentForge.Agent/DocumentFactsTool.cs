using MarqSpec.AgentForge.Data;
using MarqSpec.AgentForge.Integration.OpenEmr.Http;
using MarqSpec.AgentForge.Mcp;
using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.Agent;

/// <summary>
/// Default <see cref="IDocumentFactsTool"/>: reads the patient's <c>DerivedFact</c>s and projects each to a
/// citable <c>[Document/&lt;slug&gt;]</c> record carrying the OpenEMR source-document id, so the model can cite
/// a document the clinician can open (FR-CITE-2, gitlab#116). A fact with no value or no resolvable source
/// document is skipped — an uncitable line, not a broken overlay target. Records the access under the
/// clinician's identity, because this sidecar-native read bypasses the MCP server's audit choke point
/// (FR-AUTH-4).
/// </summary>
public sealed class DocumentFactsTool(
    IDerivedFactStore factStore,
    IClinicianIdentityAccessor clinicianIdentityAccessor,
    ICorrelationIdAccessor correlationIdAccessor,
    ILogger<DocumentFactsTool> logger) : IDocumentFactsTool
{
    /// <inheritdoc />
    public async Task<DocumentFactsResult> GetAsync(string patientId, CancellationToken cancellationToken)
    {
        // Audit before the read so a slow/failed store access is still an audited access attempt.
        AccessAuditLog.RecordAccess(
            logger,
            clinicianIdentityAccessor.ClinicianIdentity ?? "unknown",
            patientId,
            "get_document_facts",
            correlationIdAccessor.CorrelationId);

        var facts = await factStore.GetByPatientAsync(patientId, cancellationToken).ConfigureAwait(false);
        var records = new List<DocumentFactRecord>(facts.Count);
        foreach (var fact in facts)
        {
            var value = fact.Citation.QuoteOrValue;
            // The authoritative OpenEMR id wins; the citation's own SourceId is the fallback anchor.
            var documentId = fact.Document?.OpenEmrDocumentReferenceId ?? fact.Citation.SourceId;
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(documentId))
            {
                continue;
            }

            records.Add(new DocumentFactRecord(
                "Document",
                fact.Id.ToString("N")[..8],
                fact.FactType,
                value,
                documentId,
                fact.Citation.PageOrSection,
                fact.ExtractionConfidence));
        }

        return new DocumentFactsResult(records);
    }
}
