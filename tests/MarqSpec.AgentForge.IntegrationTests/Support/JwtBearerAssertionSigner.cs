using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MarqSpec.AgentForge.IntegrationTests.Support;

/// <summary>
/// Signs an RFC 7523 JWT-bearer client assertion for OpenEMR's <c>client_credentials</c> grant
/// (GitLab issue #22) - QA-harness-only, never used by production (<see cref="OpenEmrQaFixture"/>).
/// Hand-rolled rather than via a JWT library: no JWT/crypto package exists anywhere in this repo,
/// and net10.0's <see cref="RSA"/> does RS384 signing and PEM import natively.
/// </summary>
public static class JwtBearerAssertionSigner
{
    /// <summary>
    /// Builds and signs a compact JWT per RFC 7523 section 3: <c>iss</c>/<c>sub</c> are
    /// <paramref name="clientId"/>, <c>aud</c> is <paramref name="tokenEndpointUrl"/> (must match the
    /// server's own audience check exactly - OpenEMR derives it from its "Site Address Override" global,
    /// not necessarily the request's own host), and <c>exp</c> is 4 minutes out (under RFC 7523's 5-minute
    /// ceiling, leaving clock-skew margin).
    /// </summary>
    public static string CreateSignedAssertion(
        string clientId, string tokenEndpointUrl, string privateKeyPemPath, string keyId)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(privateKeyPemPath));

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = JsonSerializer.Serialize(new { alg = "RS384", typ = "JWT", kid = keyId });
        var payload = JsonSerializer.Serialize(new
        {
            iss = clientId,
            sub = clientId,
            aud = tokenEndpointUrl,
            jti = Guid.NewGuid().ToString("N"),
            iat = now,
            exp = now + 240,
        });

        var signingInput = $"{Base64Url(Encoding.UTF8.GetBytes(header))}.{Base64Url(Encoding.UTF8.GetBytes(payload))}";
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
