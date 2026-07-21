using System.ComponentModel.DataAnnotations;

namespace MarqSpec.AgentForge.Api.Launch;

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

    /// <summary>
    /// Path the browser is redirected to once an agenda session is established (ARCHITECTURE.md §19).
    /// The rendered agenda page (wwwroot/agenda.html) - it fetches the <c>/agenda</c> JSON data
    /// endpoint and renders the roster, including a friendly empty state. Distinct from that data
    /// endpoint so a launch lands on a page, not raw JSON.
    /// </summary>
    public string AgendaPath { get; init; } = "/agenda.html";

    /// <summary>
    /// Path prefix this sidecar is reached under behind a reverse proxy (e.g. <c>/agentforge</c>),
    /// or empty when root-hosted. Drives <c>UsePathBase</c> and the session cookie's <c>Path</c>/
    /// <c>SameSite</c> - non-empty means same-origin with the proxy, so <c>SameSite=Lax</c> suffices;
    /// empty is the root-hosted fallback that keeps <c>SameSite=None</c> until the None-&gt;Lax
    /// tightening lands (reference: gitlab#63).
    /// </summary>
    public string PathBase { get; init; } = string.Empty;

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

        if (PathBase.Length > 0 && (!PathBase.StartsWith('/') || PathBase.EndsWith('/')))
        {
            yield return new ValidationResult(
                $"{nameof(PathBase)} must start with '/' and must not end with '/' (e.g. '/agentforge').",
                [nameof(PathBase)]);
        }
    }
}
