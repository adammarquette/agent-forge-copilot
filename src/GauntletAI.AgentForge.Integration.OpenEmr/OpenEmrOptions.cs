using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Integration.OpenEmr;

/// <summary>
/// OpenEMR connection and OAuth client configuration, bound via the Options pattern
/// (ENGINEERING_STANDARDS.md §6). Sourced from environment variables layered over
/// appsettings.{Environment}.json - never hard-coded, never committed with real values.
/// </summary>
public sealed class OpenEmrOptions : IValidatableObject
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "OpenEmr";

    /// <summary>Default per-attempt FHIR timeout (seconds); see <see cref="FhirAttemptTimeoutSeconds"/>.</summary>
    public const int DefaultFhirAttemptTimeoutSeconds = 30;

    /// <summary>Default total FHIR request timeout (seconds); see <see cref="FhirTotalRequestTimeoutSeconds"/>.</summary>
    public const int DefaultFhirTotalRequestTimeoutSeconds = 90;

    /// <summary>Base URL of the deployed OpenEMR instance. Must be HTTPS (ENGINEERING_STANDARDS.md §4).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string BaseUrl { get; init; }

    /// <summary>OpenEMR multi-site segment, e.g. "default" (INTERFACE_CONTROL.md §0).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Site { get; init; }

    /// <summary>OAuth2 client id from dynamic client registration (INTERFACE_CONTROL.md A.3).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string ClientId { get; init; }

    /// <summary>OAuth2 client secret, if the registered client is confidential rather than public.</summary>
    public string? ClientSecret { get; init; }

    /// <summary>Least-privilege scopes requested on the SMART EHR launch (INTERFACE_CONTROL.md A.4).</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>
    /// Permits a non-HTTPS <see cref="BaseUrl"/>. Must be true only on an isolated local
    /// development path - never in QA/prod (ENGINEERING_STANDARDS.md §4, §11).
    /// </summary>
    public bool AllowInsecureHttpForLocalDevelopment { get; init; }

    /// <summary>
    /// Per-attempt timeout (seconds) for a single OpenEMR FHIR call. Default 30s: staging OpenEMR
    /// routinely takes 4-8s per call and the framework's 10s HTTP default trips under the Daily
    /// Agenda's parallel fan-out, forcing retries/cancellations (reference: gitlab#80). Governs the
    /// FHIR client's resilience-handler AttemptTimeout.
    /// </summary>
    public int FhirAttemptTimeoutSeconds { get; init; } = DefaultFhirAttemptTimeoutSeconds;

    /// <summary>
    /// Total timeout (seconds) across all retries for one OpenEMR FHIR call. Default 90s. Must be
    /// strictly greater than a single attempt or the standard resilience handler throws at startup;
    /// governs TotalRequestTimeout.
    /// </summary>
    public int FhirTotalRequestTimeoutSeconds { get; init; } = DefaultFhirTotalRequestTimeoutSeconds;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FhirAttemptTimeoutSeconds <= 0)
        {
            yield return new ValidationResult(
                $"{nameof(FhirAttemptTimeoutSeconds)} must be greater than zero.",
                [nameof(FhirAttemptTimeoutSeconds)]);
        }

        if (FhirTotalRequestTimeoutSeconds <= FhirAttemptTimeoutSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(FhirTotalRequestTimeoutSeconds)} must be greater than {nameof(FhirAttemptTimeoutSeconds)}.",
                [nameof(FhirTotalRequestTimeoutSeconds)]);
        }

        if (Scopes is null || Scopes.Count == 0)
        {
            // Scopes is `required`, but that's a compile-time-only guarantee - IConfiguration
            // binding via reflection leaves it genuinely null when the config section carries no
            // Scopes key at all, rather than an empty list (confirmed live: this exact unguarded
            // check threw NullReferenceException in production for the sibling AgendaOpenEmrOptions).
            yield return new ValidationResult(
                "At least one OAuth scope is required.",
                [nameof(Scopes)]);
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
        {
            yield return new ValidationResult(
                $"{nameof(BaseUrl)} must be an absolute URL.",
                [nameof(BaseUrl)]);
        }
        else if (uri.Scheme != Uri.UriSchemeHttps && !AllowInsecureHttpForLocalDevelopment)
        {
            yield return new ValidationResult(
                $"{nameof(BaseUrl)} must use https:// unless {nameof(AllowInsecureHttpForLocalDevelopment)} " +
                "is explicitly set (ENGINEERING_STANDARDS.md §4).",
                [nameof(BaseUrl)]);
        }
    }
}
