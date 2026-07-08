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

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Scopes.Count == 0)
        {
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
