namespace GauntletAI.AgentForge.Data.Entities;

/// <summary>
/// A single fact extracted from an <see cref="IngestedDocument"/>. <b>Sidecar-authoritative</b> — there is no
/// supported FHIR/REST create path for these, so they live here, each citing the OpenEMR source document
/// (W2_ARCHITECTURE.md §4). This is PHI at rest; treat accordingly (§12).
/// </summary>
public sealed class DerivedFact
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key to the owning <see cref="IngestedDocument"/>.</summary>
    public Guid IngestedDocumentId { get; set; }

    /// <summary>The document this fact was derived from; navigation property.</summary>
    public IngestedDocument? Document { get; set; }

    /// <summary>Fact category (e.g. <c>lab.result</c>, <c>intake.medication</c>).</summary>
    public required string FactType { get; set; }

    /// <summary>The strict-schema fact payload as JSON (stored <c>jsonb</c>); the schema is the contract (NFR-CONTRACT-1).</summary>
    public required string PayloadJson { get; set; }

    /// <summary>Where this fact came from — resolves back to the source (owned value object).</summary>
    public required Citation Citation { get; set; }

    /// <summary>Extractor confidence in <c>[0,1]</c> when reported; null if not provided.</summary>
    public double? ExtractionConfidence { get; set; }

    /// <summary>When the fact was persisted.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
