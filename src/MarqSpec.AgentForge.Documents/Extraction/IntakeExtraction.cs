namespace MarqSpec.AgentForge.Documents.Extraction;

/// <summary>
/// Strict extraction schema for a patient intake form (Week 2 Core Req 2). Same gate as
/// <see cref="LabExtraction"/> — the schema is the contract; unschematized model output is rejected.
/// </summary>
public sealed record IntakeExtraction
{
    /// <summary>Patient demographics captured on the form.</summary>
    public required IntakeDemographics Demographics { get; init; }

    /// <summary>The chief concern / reason for visit, when stated.</summary>
    public string? ChiefConcern { get; init; }

    /// <summary>Current medications the patient reports.</summary>
    public required IReadOnlyList<IntakeMedication> CurrentMedications { get; init; }

    /// <summary>Reported allergies (free-text items).</summary>
    public required IReadOnlyList<string> Allergies { get; init; }

    /// <summary>Reported family history (free-text items).</summary>
    public required IReadOnlyList<string> FamilyHistory { get; init; }

    /// <summary>Where the form's core fields were read from (required — the grounding gate).</summary>
    public required ExtractionCitation Citation { get; init; }
}

/// <summary>Demographic fields from an intake form; individual fields are nullable when not present.</summary>
public sealed record IntakeDemographics
{
    /// <summary>Full name as written.</summary>
    public string? FullName { get; init; }

    /// <summary>Date of birth as written / ISO-8601 when unambiguous.</summary>
    public string? DateOfBirth { get; init; }

    /// <summary>Sex/gender as written.</summary>
    public string? Sex { get; init; }
}

/// <summary>One reported medication with an optional dose, source-cited.</summary>
public sealed record IntakeMedication
{
    /// <summary>Medication name as written.</summary>
    public required string Name { get; init; }

    /// <summary>Dose/frequency as written, when present.</summary>
    public string? Dose { get; init; }

    /// <summary>Where this medication was read from (required — the grounding gate).</summary>
    public required ExtractionCitation Citation { get; init; }
}
