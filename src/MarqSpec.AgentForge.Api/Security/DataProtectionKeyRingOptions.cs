using System.ComponentModel.DataAnnotations;

namespace MarqSpec.AgentForge.Api.Security;

/// <summary>
/// ASP.NET Core DataProtection key-ring configuration, bound via the Options pattern
/// (ENGINEERING_STANDARDS.md §6). The session cookie that carries the pending SMART-launch state
/// (state + PKCE verifier) is encrypted with these keys; if the key ring is the framework default
/// (in-memory, regenerated per process), a redeploy or a second replica cannot decrypt a cookie an
/// earlier process wrote, the session reads as empty, and the launch callback fails with
/// "No pending SMART launch for this session" (confirmed live 2026-07-12). Persisting the key ring
/// to shared, durable storage keeps launches whole across restarts and replicas.
/// </summary>
public sealed class DataProtectionKeyRingOptions : IValidatableObject
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Absolute filesystem path of the persisted key ring (a mounted, durable volume in deployed
    /// environments). Empty keeps the framework default in-memory key ring - acceptable only for
    /// local development and unit tests, never for a deployed environment.
    /// </summary>
    public string KeyRingPath { get; init; } = string.Empty;

    /// <summary>
    /// DataProtection application discriminator. Pinning it keeps keys stable across restarts even
    /// when the content-root path changes, and isolates this app's keys from any other app sharing
    /// the same store.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ApplicationName { get; init; } = "agent-forge-copilot";

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // A relative path resolves against the process working directory - not persisted and
        // per-process - which silently reintroduces the ephemeral-key bug this option prevents.
        if (KeyRingPath.Length > 0 && !Path.IsPathRooted(KeyRingPath))
        {
            yield return new ValidationResult(
                $"{nameof(KeyRingPath)} must be an absolute (rooted) path, e.g. '/keys', when set.",
                [nameof(KeyRingPath)]);
        }
    }
}
