namespace GauntletAI.AgentForge.Evals;

/// <summary>One golden-set eval case. Deterministic: the stubbed model response is fixed, so the case's
/// pass/fail is reproducible and a regression in the pipeline flips it (the hard-gate mechanism).</summary>
internal sealed record GoldenCase
{
    public required string Id { get; init; }

    /// <summary>Capability under test, e.g. "extraction".</summary>
    public required string Category { get; init; }

    /// <summary>"lab_pdf" or "intake_form".</summary>
    public required string DocType { get; init; }

    /// <summary>IANA media type of the (synthetic) document.</summary>
    public string MediaType { get; init; } = "application/pdf";

    /// <summary>The response the (stubbed) vision model returns for this case.</summary>
    public required string StubModelResponse { get; init; }

    /// <summary>Whether extraction is expected to pass the schema gate.</summary>
    public required bool ExpectSuccess { get; init; }

    /// <summary>Substrings that must appear in the canonical extraction (factually_consistent).</summary>
    public IReadOnlyList<string>? ExpectedValues { get; init; }

    /// <summary>Values that must NEVER appear in logs for this case (no_phi_in_logs).</summary>
    public IReadOnlyList<string>? PhiTokens { get; init; }

    /// <summary>Which boolean rubrics apply to this case.</summary>
    public required IReadOnlyList<string> Rubrics { get; init; }
}

/// <summary>The committed baseline the gate compares against.</summary>
internal sealed record EvalBaseline
{
    /// <summary>Minimum acceptable pass rate for any category (absolute floor).</summary>
    public double PassThreshold { get; init; } = 1.0;

    /// <summary>Maximum tolerated drop from a category's baseline before the build fails.</summary>
    public double MaxRegression { get; init; } = 0.05;

    /// <summary>Per-category baseline pass rates.</summary>
    public required IReadOnlyDictionary<string, double> Categories { get; init; }
}
