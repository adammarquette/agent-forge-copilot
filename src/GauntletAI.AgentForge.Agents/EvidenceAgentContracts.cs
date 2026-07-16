using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.Agents;

/// <summary>A document awaiting extraction as part of an evidence-agent request.</summary>
/// <param name="DocumentType">Which strict schema to extract to.</param>
/// <param name="Content">Raw document bytes.</param>
/// <param name="MediaType">IANA media type (e.g. <c>application/pdf</c>).</param>
public sealed record PendingDocument(ClinicalDocumentType DocumentType, ReadOnlyMemory<byte> Content, string MediaType);

/// <summary>Input to the Week 2 evidence agent (W2_ARCHITECTURE.md §6).</summary>
public sealed record EvidenceAgentRequest
{
    /// <summary>The patient the question concerns.</summary>
    public required string PatientId { get; init; }

    /// <summary>The clinician's question.</summary>
    public required string Question { get; init; }

    /// <summary>An optional document to extract this turn; null when facts are already on file.</summary>
    public PendingDocument? Document { get; init; }
}

/// <summary>One inspectable handoff between graph nodes (Week 2: handoffs must be logged and explainable).</summary>
/// <param name="From">Node handing off.</param>
/// <param name="To">Node receiving.</param>
/// <param name="Reason">Why the supervisor routed this way.</param>
public sealed record HandoffEvent(string From, string To, string Reason);

/// <summary>Result of one evidence-agent run, after the critic gate.</summary>
public sealed record EvidenceAgentResult
{
    /// <summary>The verified, cited answer (uncited claims already suppressed).</summary>
    public required string Answer { get; init; }

    /// <summary>Cardiology domain-constraint flags surfaced by the critic.</summary>
    public required IReadOnlyList<DomainConstraintFlag> SafetyFlags { get; init; }

    /// <summary>Claims the critic suppressed for failing source attribution.</summary>
    public required IReadOnlyList<SuppressedClaim> SuppressedClaims { get; init; }

    /// <summary>The ordered, logged handoffs — the inspectable routing trace.</summary>
    public required IReadOnlyList<HandoffEvent> Handoffs { get; init; }

    /// <summary>The extracted-facts JSON used this turn; null if no document was extracted.</summary>
    public string? ExtractedFactsJson { get; init; }

    /// <summary>Structured click-to-source citations (page + bbox + quote) for facts derived from a document attached this turn (FR-CITE-2); empty when none.</summary>
    public IReadOnlyList<DocumentCitation> DocumentCitations { get; init; } = [];

    /// <summary>The guideline evidence retrieved this turn.</summary>
    public required IReadOnlyList<EvidenceSnippet> Evidence { get; init; }
}
