using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// The BFF's own public-facing configuration (ENGINEERING_STANDARDS.md §6) - distinct from
/// <c>OpenEmrOptions</c>, which describes the connection *to* OpenEMR, not this sidecar's own
/// externally-reachable address.
/// </summary>
public sealed class BffOptions : IValidatableObject
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "Bff";

    /// <summary>
    /// This sidecar's own public base URL, used to build the OAuth <c>redirect_uri</c> registered
    /// for the SMART EHR launch client. Must be HTTPS (ENGINEERING_STANDARDS.md §4, §11).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string PublicBaseUrl { get; init; }

    /// <summary>Path the OpenEMR authorization server redirects back to after the launch.</summary>
    public string CallbackPath { get; init; } = "/callback";

    /// <summary>Path of the hosted chat SPA the browser is redirected to once a session is established.</summary>
    public string ChatPath { get; init; } = "/index.html";

    /// <summary>Path the browser is redirected to once an agenda session is established (ARCHITECTURE.md §19).</summary>
    public string AgendaPath { get; init; } = "/agenda";

    /// <summary>
    /// Permits a non-HTTPS <see cref="PublicBaseUrl"/>. Must be true only on an isolated local
    /// development path - never in QA/prod (ENGINEERING_STANDARDS.md §4, §11).
    /// </summary>
    public bool AllowInsecureHttpForLocalDevelopment { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri))
        {
            yield return new ValidationResult(
                $"{nameof(PublicBaseUrl)} must be an absolute URL.",
                [nameof(PublicBaseUrl)]);
        }
        else if (uri.Scheme != Uri.UriSchemeHttps && !AllowInsecureHttpForLocalDevelopment)
        {
            yield return new ValidationResult(
                $"{nameof(PublicBaseUrl)} must use https:// unless {nameof(AllowInsecureHttpForLocalDevelopment)} " +
                "is explicitly set (ENGINEERING_STANDARDS.md §4).",
                [nameof(PublicBaseUrl)]);
        }
    }
}
