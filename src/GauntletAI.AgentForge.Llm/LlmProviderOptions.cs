using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Llm;

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

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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
