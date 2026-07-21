using System.Security.Cryptography;
using System.Text;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Generates RFC 7636 PKCE verifier/challenge pairs for the SMART EHR launch
/// authorization-code flow (INTERFACE_CONTROL.md Interface A.3).
/// </summary>
public static class PkceGenerator
{
    private const string UnreservedCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    private const int MinVerifierLength = 43;
    private const int MaxVerifierLength = 128;
    private const int DefaultVerifierLength = 64;

    /// <summary>
    /// Generates a new, cryptographically random PKCE pair using the S256 challenge method.
    /// </summary>
    /// <param name="verifierLength">
    /// Length of the generated <c>code_verifier</c>. Must be within the RFC 7636 bounds of
    /// 43-128 characters.
    /// </param>
    public static PkceChallenge Generate(int verifierLength = DefaultVerifierLength)
    {
        if (verifierLength is < MinVerifierLength or > MaxVerifierLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verifierLength),
                verifierLength,
                $"RFC 7636 requires a code_verifier between {MinVerifierLength} and {MaxVerifierLength} characters.");
        }

        var codeVerifier = RandomNumberGenerator.GetString(UnreservedCharacters, verifierLength);
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new PkceChallenge(codeVerifier, codeChallenge, "S256");
    }
}
