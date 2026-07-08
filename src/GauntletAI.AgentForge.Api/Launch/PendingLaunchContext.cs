namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// The CSRF state and PKCE verifier minted for one in-flight SMART launch, held server-side (in
/// the browser session, never the browser itself) between the initial redirect and the callback.
/// </summary>
/// <param name="State">Opaque value echoed back by the authorization server; verified at the callback.</param>
/// <param name="CodeVerifier">RFC 7636 PKCE verifier, exchanged for the token at the callback.</param>
public sealed record PendingLaunchContext(string State, string CodeVerifier);
