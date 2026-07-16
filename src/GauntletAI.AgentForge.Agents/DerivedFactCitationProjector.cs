using System.Globalization;
using GauntletAI.AgentForge.Data.Entities;

namespace GauntletAI.AgentForge.Agents;

/// <summary>
/// Projects persisted <see cref="DerivedFact"/> rows into click-to-source citations for the production
/// overlay (FR-CITE-2, gitlab#109). Each citation carries the OpenEMR <c>DocumentReference</c> id
/// (<see cref="DocumentCitation.SourceDocumentId"/>) so the client can fetch the source PDF, plus the page and
/// bounding box. A fact with no resolvable document or no cited value is skipped — an uncitable line, not a
/// broken overlay target.
/// </summary>
public static class DerivedFactCitationProjector
{
    /// <summary>Turns the patient's persisted facts into fetchable citations; skips any that can't be sourced.</summary>
    public static IReadOnlyList<DocumentCitation> Project(IReadOnlyList<DerivedFact> facts)
    {
        var citations = new List<DocumentCitation>(facts.Count);
        foreach (var fact in facts)
        {
            // Document navigation wins (the authoritative OpenEMR id); the citation's SourceId is the fallback.
            var documentId = fact.Document?.OpenEmrDocumentReferenceId;
            if (string.IsNullOrWhiteSpace(documentId))
            {
                documentId = fact.Citation.SourceId;
            }

            var value = fact.Citation.QuoteOrValue;
            if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var page = int.TryParse(fact.Citation.PageOrSection, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p)
                ? p
                : 1;

            citations.Add(new DocumentCitation(
                fact.Id.ToString("N")[..8],
                fact.FactType,
                value,
                page,
                fact.Citation.BoundingBox,
                fact.Citation.QuoteOrValue,
                documentId));
        }

        return citations;
    }
}
