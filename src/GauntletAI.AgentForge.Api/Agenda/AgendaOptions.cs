using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Api.Agenda;

/// <summary>
/// Daily Agenda fan-out configuration, bound via the Options pattern (ENGINEERING_STANDARDS.md
/// §6, ARCHITECTURE.md §19.4).
/// </summary>
public sealed class AgendaOptions : IValidatableObject
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "Agenda";

    /// <summary>
    /// Bounds how many per-patient summary turns run concurrently - protects both the LLM
    /// provider's rate limits and OpenEMR against a full ~20-30-patient panel fired unbounded in
    /// one request.
    /// </summary>
    public int MaxConcurrentSummaries { get; init; } = 4;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxConcurrentSummaries <= 0)
        {
            yield return new ValidationResult(
                $"{nameof(MaxConcurrentSummaries)} must be positive.", [nameof(MaxConcurrentSummaries)]);
        }
    }
}
