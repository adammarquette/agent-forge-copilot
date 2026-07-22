namespace MarqSpec.AgentForge.Documents.Extraction;

/// <summary>
/// Strict extraction schema for a lab PDF (Week 2 Core Req 2). The schema — not whatever the model happens
/// to return — is the source of truth (NFR-CONTRACT-1): a payload missing a required field fails
/// deserialization and the extraction is rejected, nothing persisted.
/// </summary>
public sealed record LabExtraction
{
    /// <summary>The individual test results read from the report.</summary>
    public required IReadOnlyList<LabTestResult> Tests { get; init; }
}

/// <summary>One laboratory result with the fields the cardiologist needs, each source-cited.</summary>
public sealed record LabTestResult
{
    /// <summary>Test name (e.g. "INR", "Potassium", "Creatinine").</summary>
    public required string TestName { get; init; }

    /// <summary>The measured value, as printed (kept as text so units/precision aren't lost).</summary>
    public required string Value { get; init; }

    /// <summary>Unit of measure, when present (e.g. "mmol/L").</summary>
    public string? Unit { get; init; }

    /// <summary>Reference range as printed (e.g. "3.5-5.1"), when present.</summary>
    public string? ReferenceRange { get; init; }

    /// <summary>Collection date as printed / ISO-8601 when unambiguous, when present.</summary>
    public string? CollectionDate { get; init; }

    /// <summary>Whether the report flags the value out of range; null if not indicated.</summary>
    public bool? AbnormalFlag { get; init; }

    /// <summary>Where this result was read from (required — the grounding gate).</summary>
    public required ExtractionCitation Citation { get; init; }
}
