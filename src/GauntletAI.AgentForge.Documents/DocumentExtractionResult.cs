using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Llm;

namespace GauntletAI.AgentForge.Documents;

/// <summary>
/// Outcome of a document extraction. Either the model's output passed the strict schema
/// (<see cref="Succeeded"/> = true, <see cref="CanonicalJson"/> set) or it was rejected at the boundary
/// (<see cref="RejectionReason"/> set) and nothing should be persisted — the "vision without invention" gate.
/// </summary>
public sealed record DocumentExtractionResult
{
    /// <summary>Whether the model output validated against the strict schema.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Which document type was extracted.</summary>
    public required ClinicalDocumentType DocumentType { get; init; }

    /// <summary>The validated, re-serialized (canonical) extraction JSON; null when rejected.</summary>
    public string? CanonicalJson { get; init; }

    /// <summary>Why the extraction was rejected; null when succeeded. PHI-free.</summary>
    public string? RejectionReason { get; init; }

    /// <summary>Token/cost accounting for the extraction call, for observability; null if unavailable.</summary>
    public LlmUsage? Usage { get; init; }

    /// <summary>Builds a successful result.</summary>
    public static DocumentExtractionResult Ok(ClinicalDocumentType type, string canonicalJson, LlmUsage usage) =>
        new() { Succeeded = true, DocumentType = type, CanonicalJson = canonicalJson, Usage = usage };

    /// <summary>Builds a rejected result (schema failure, empty payload, etc.).</summary>
    public static DocumentExtractionResult Rejected(ClinicalDocumentType type, string reason, LlmUsage? usage = null) =>
        new() { Succeeded = false, DocumentType = type, RejectionReason = reason, Usage = usage };
}
