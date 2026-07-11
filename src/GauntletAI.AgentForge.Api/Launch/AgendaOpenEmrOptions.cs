using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// OAuth client configuration for the roster-level Daily Agenda launch (ARCHITECTURE.md §19),
/// bound via the Options pattern (ENGINEERING_STANDARDS.md §6). Deliberately separate from
/// <c>OpenEmrOptions</c> rather than a wider <c>Scopes</c> list under the same client id: the
/// fork's <c>ScopeRepository::finalizeScopes()</c> silently drops any requested scope the
/// registered client didn't register with (confirmed against source, INTERFACE_CONTROL.md A.4) -
/// reusing <c>OpenEmrOptions.ClientId</c> here would silently narrow the granted token instead of
/// failing loudly. <c>BaseUrl</c>/<c>Site</c> are still shared with <c>OpenEmrOptions</c> - same
/// OpenEMR deployment, just a different registered client.
/// </summary>
public sealed class AgendaOpenEmrOptions : IValidatableObject
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "OpenEmrAgenda";

    /// <summary>OAuth2 client id for the agenda's own registered client (INTERFACE_CONTROL.md A.4).</summary>
    [Required(AllowEmptyStrings = false)]
    public required string ClientId { get; init; }

    /// <summary>OAuth2 client secret, if the registered client is confidential rather than public.</summary>
    public string? ClientSecret { get; init; }

    /// <summary>Provider-wide (<c>user/*.read</c>) scopes for the roster launch (INTERFACE_CONTROL.md A.4).</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Scopes.Count == 0)
        {
            yield return new ValidationResult(
                "At least one OAuth scope is required.",
                [nameof(Scopes)]);
        }
    }
}
