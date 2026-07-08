namespace GauntletAI.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// An RFC 7636 PKCE verifier/challenge pair for the SMART EHR launch authorization-code flow.
/// </summary>
/// <param name="CodeVerifier">The secret, sent as <c>code_verifier</c> on the token exchange.</param>
/// <param name="CodeChallenge">The SHA-256/base64url digest of <paramref name="CodeVerifier"/>, sent as <c>code_challenge</c> on the authorize request.</param>
/// <param name="CodeChallengeMethod">Always <c>S256</c>; plain-text PKCE is not supported.</param>
public sealed record PkceChallenge(string CodeVerifier, string CodeChallenge, string CodeChallengeMethod);
