using GauntletAI.AgentForge.Integration.OpenEmr.Http;

namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// Holds the current session's access token for the lifetime of one hub invocation, so
/// <see cref="AuthHandler"/> can attach it to outbound FHIR calls without ever routing the token
/// through the browser (ARCHITECTURE.md D11).
/// </summary>
/// <remarks>
/// Backed by a <c>static</c> <see cref="AsyncLocal{T}"/>, not a per-instance field, even though
/// this type is registered Scoped (GitLab issue #39): <see cref="AuthHandler"/> is constructed by
/// <c>IHttpClientFactory</c> using its own internal handler-building scope, never the calling hub
/// invocation's DI scope - confirmed live 2026-07-10, a plain per-instance field left
/// <see cref="AuthHandler"/> reading a <em>different, never-set</em> instance no matter how often
/// the handler was rebuilt (<c>SetHandlerLifetime</c> alone does not fix this). <see cref="AsyncLocal{T}"/>
/// sidesteps DI scoping entirely: it flows correctly through the actual async call chain (hub
/// method -&gt; orchestrator -&gt; tool dispatch -&gt; <see cref="AuthHandler.SendAsync"/>) regardless
/// of which DI scope constructed which object along the way, and - just as importantly - stays
/// isolated per logical flow, so concurrent hub invocations for different clinicians can never
/// observe each other's token.
/// </remarks>
public sealed class ScopedAccessTokenProvider : IScopedAccessTokenProvider
{
    private static readonly AsyncLocal<string?> AmbientAccessToken = new();

    /// <inheritdoc />
    public string? AccessToken
    {
        get => AmbientAccessToken.Value;
        set => AmbientAccessToken.Value = value;
    }

    /// <inheritdoc />
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(AccessToken);
}
