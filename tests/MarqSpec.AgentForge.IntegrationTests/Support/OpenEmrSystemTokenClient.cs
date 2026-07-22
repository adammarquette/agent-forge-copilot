using System.Text.Json;

namespace MarqSpec.AgentForge.IntegrationTests.Support;

/// <summary>
/// Mints an OpenEMR access token via the <c>client_credentials</c> grant + JWT-bearer client assertion
/// (RFC 7523, GitLab issue #22) - the durable replacement for a manually re-minted, hour-lived
/// <c>OpenEmrQa__TestAccessToken</c>. QA-harness-only: deliberately a raw <see cref="HttpClient"/>, not
/// the production <c>IOpenEmrAuthApi</c>/<c>TokenRequest</c> Refit types, which stay authorization_code-only
/// by design (<c>src/AGENTS.md</c> - client_credentials is out of v1 production scope, ARCHITECTURE.md §18.2).
/// </summary>
public static class OpenEmrSystemTokenClient
{
    /// <summary>
    /// Signs a fresh assertion and exchanges it for an access token scoped to <paramref name="scope"/>.
    /// Throws with the server's status/body on failure so a misconfigured or disabled client is
    /// diagnosable from CI logs rather than a bare stack trace.
    /// </summary>
    public static async Task<string> MintAccessTokenAsync(
        string baseUrl,
        string site,
        string clientId,
        string privateKeyPemPath,
        string keyId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var tokenEndpoint = $"{baseUrl.TrimEnd('/')}/oauth2/{site}/token";
        var assertion = JwtBearerAssertionSigner.CreateSignedAssertion(clientId, tokenEndpoint, privateKeyPemPath, keyId);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion,
            ["scope"] = scope,
        };

        using var http = new HttpClient();
        using var response = await http.PostAsync(
            tokenEndpoint, new FormUrlEncodedContent(form), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenEMR client_credentials token mint failed: {(int)response.StatusCode} {response.StatusCode} - {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException($"OpenEMR token response carried no access_token: {body}");
    }
}
