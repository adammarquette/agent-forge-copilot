using MarqSpec.AgentForge.Integration.OpenEmr.Http;

namespace MarqSpec.AgentForge.Api.Session;

/// <summary>
/// The settable half of <see cref="IAccessTokenProvider"/> - a hub method or endpoint populates
/// <see cref="AccessToken"/> once from the session at the start of a call, and every downstream
/// OpenEMR call within that same DI scope reads it back through <see cref="IAccessTokenProvider"/>.
/// </summary>
public interface IScopedAccessTokenProvider : IAccessTokenProvider
{
    /// <summary>The token to hand out for the remainder of this scope, or <see langword="null"/> if unset.</summary>
    string? AccessToken { get; set; }
}
