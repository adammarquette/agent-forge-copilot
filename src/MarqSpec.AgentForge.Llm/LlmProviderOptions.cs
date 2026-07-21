using System.ComponentModel.DataAnnotations;

namespace MarqSpec.AgentForge.Llm;

/// <summary>
/// LLM provider configuration, bound via the Options pattern (ENGINEERING_STANDARDS.md §6).
/// Pricing is configured rather than hard-coded: it changes independently of a code release, and
/// PRD.md §15.1's grounding rule requires cost figures to come from measurement/configuration,
/// not an assumed-correct constant baked into source.
/// </summary>
public sealed class LlmProviderOptions : IValidatableObject
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "Llm";

    /// <summary>Default per-attempt LLM timeout (seconds); see <see cref="AttemptTimeoutSeconds"/>.</summary>
    public const int DefaultAttemptTimeoutSeconds = 60;

    /// <summary>Default total LLM request timeout (seconds); see <see cref="TotalRequestTimeoutSeconds"/>.</summary>
    public const int DefaultTotalRequestTimeoutSeconds = 150;

    /// <summary>Provider API key. Never logged, never in source (ENGINEERING_STANDARDS.md §11).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; init; }

    /// <summary>Model identifier, e.g. a Sonnet-class model (ARCHITECTURE.md §12).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Model { get; init; }

    /// <summary>Provider API base URL.</summary>
    public string BaseUrl { get; init; } = "https://api.anthropic.com";

    /// <summary>Published input-token price, for the cost estimate in <see cref="LlmUsage"/>.</summary>
    public required decimal InputPricePerMillionTokensUsd { get; init; }

    /// <summary>Published output-token price, for the cost estimate in <see cref="LlmUsage"/>.</summary>
    public required decimal OutputPricePerMillionTokensUsd { get; init; }

    /// <summary>
    /// Per-attempt timeout (seconds) for a single LLM HTTP call. Default 60s: the framework's 10s HTTP
    /// default is too tight for the heaviest agenda-synthesis prompt, forcing a deterministic-fallback
    /// degrade (reference: gitlab#77). Governs the resilience handler's AttemptTimeout.
    /// </summary>
    public int AttemptTimeoutSeconds { get; init; } = DefaultAttemptTimeoutSeconds;

    /// <summary>
    /// Total timeout (seconds) across all retries for one LLM request. Default 150s. Must be strictly
    /// greater than a single attempt or the standard resilience handler throws at startup; governs
    /// TotalRequestTimeout.
    /// </summary>
    public int TotalRequestTimeoutSeconds { get; init; } = DefaultTotalRequestTimeoutSeconds;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AttemptTimeoutSeconds <= 0)
        {
            yield return new ValidationResult(
                $"{nameof(AttemptTimeoutSeconds)} must be greater than zero.",
                [nameof(AttemptTimeoutSeconds)]);
        }

        if (TotalRequestTimeoutSeconds <= AttemptTimeoutSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(TotalRequestTimeoutSeconds)} must be greater than {nameof(AttemptTimeoutSeconds)}.",
                [nameof(TotalRequestTimeoutSeconds)]);
        }

        if (InputPricePerMillionTokensUsd < 0)
        {
            yield return new ValidationResult(
                $"{nameof(InputPricePerMillionTokensUsd)} cannot be negative.",
                [nameof(InputPricePerMillionTokensUsd)]);
        }

        if (OutputPricePerMillionTokensUsd < 0)
        {
            yield return new ValidationResult(
                $"{nameof(OutputPricePerMillionTokensUsd)} cannot be negative.",
                [nameof(OutputPricePerMillionTokensUsd)]);
        }
    }
}
