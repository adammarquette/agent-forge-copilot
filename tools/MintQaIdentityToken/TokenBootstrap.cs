using MarqSpec.AgentForge.Integration.OpenEmr.Auth;

namespace MarqSpec.AgentForge.MintQaIdentityToken;

/// <summary>
/// Mints a second, real patient-scoped OpenEMR access token via a genuine SMART standalone-launch
/// <c>authorization_code</c> + PKCE login (GitLab issue #27) - the entitlement
/// <c>CrossIdentityAuthorizationTests</c> needs a distinct identity to test can only come from a real
/// login, the same way the original (now-superseded) <c>OpenEmrQa__TestAccessToken</c> was obtained.
/// Unlike <c>tools/SeedDemoPatients</c>' <c>AuthBootstrap</c> (a <c>user/*</c>-scoped client that can
/// see every patient - fine for writing demo data), this requests <c>patient/patient.read</c> +
/// <c>launch/patient</c> so the resulting token is genuinely restricted to whichever single patient
/// the login/consent step resolves to (confirmed via the token response's own <c>patient</c> claim,
/// not assumed).
/// </summary>
public static class TokenBootstrap
{
    private const string RedirectUri = "https://sidecar.invalid/callback";

    // PascalCase resource name is required - confirmed live against the QA server
    // (INTERFACE_CONTROL.md A.4's casing note): "patient/patient.read" is rejected as
    // invalid_scope at both /registration and /authorize; only "patient/Patient.read" is accepted
    // (confirmed exhaustively via the live .well-known/openid-configuration's scopes_supported).
    // Condition/MedicationRequest/AllergyIntolerance are needed alongside Patient because
    // McpToolServer.GetPatientSummaryAsync - what CrossIdentityAuthorizationTests exercises -
    // fetches all four resources, not just Patient. offline_access (confirmed supported in the live
    // .well-known/openid-configuration's scopes_supported) is what actually gets a refresh_token back
    // in the token response - without it the server issues access-token-only, silent about the
    // omission (GitLab issue #29: makes these tokens durable instead of needing re-minting hourly).
    private static readonly string[] Scopes =
    [
        "openid", "fhirUser", "launch/patient", "api:fhir", "offline_access",
        "patient/Patient.read", "patient/Condition.read", "patient/MedicationRequest.read", "patient/AllergyIntolerance.read",
    ];

    /// <summary>
    /// Registers a fresh public client (or reuses <c>MintToken__ClientId</c>/<c>ClientSecret</c> from
    /// the environment if set). Registered clients land disabled - the operator must click Enable
    /// (Admin -&gt; System -&gt; API Clients) before the login step below will work.
    /// </summary>
    public static async Task<(string ClientId, string? ClientSecret)> ResolveClientAsync(
        IOpenEmrAuthClient authClient, string site, CancellationToken cancellationToken = default)
    {
        var envClientId = Environment.GetEnvironmentVariable("MintToken__ClientId");
        if (!string.IsNullOrWhiteSpace(envClientId))
        {
            Console.WriteLine("Using MintToken__ClientId from the environment - skipping registration.");
            return (envClientId, Environment.GetEnvironmentVariable("MintToken__ClientSecret"));
        }

        var registration = await authClient.RegisterPublicClientAsync(
            site, "AgentForge QA Identity B Token Minter", [RedirectUri], Scopes, cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Registered client '{registration.ClientId}'.");
        Console.WriteLine("Open Admin -> System -> API Clients, find 'AgentForge QA Identity B Token Minter',");
        Console.WriteLine("click Enable, then press Enter.");
        Console.ReadLine();

        return (registration.ClientId, registration.ClientSecret);
    }

    /// <summary>
    /// Walks the operator through a real browser login/consent, then exchanges the resulting code for
    /// a token. The access token, its granted scope, and its <c>patient</c> claim all come straight
    /// from OpenEMR's own token response - nothing here assumes which patient gets selected.
    /// </summary>
    public static async Task<TokenResponse> AcquireTokenAsync(
        IOpenEmrAuthClient authClient, string baseUrl, string site, string clientId, string? clientSecret,
        CancellationToken cancellationToken = default)
    {
        var pkce = PkceGenerator.Generate();
        var state = Guid.NewGuid().ToString("N");
        var authorizeUrl = AuthorizeUrlBuilder.Build(new AuthorizeRequest(
            AuthorizeEndpoint: $"{baseUrl.TrimEnd('/')}/oauth2/{site}/authorize",
            ClientId: clientId,
            RedirectUri: RedirectUri,
            Scopes: Scopes,
            State: state,
            Pkce: pkce,
            Aud: $"{baseUrl.TrimEnd('/')}/apis/{site}/fhir"));

        Console.WriteLine();
        Console.WriteLine("Open this URL and log in as (or select) the SECOND, distinct patient identity - a");
        Console.WriteLine("different patient than OpenEmrQa__TestPatientId - and approve:");
        Console.WriteLine(authorizeUrl);
        Console.WriteLine();
        Console.WriteLine("The redirect will fail to load (sidecar.invalid isn't a real host) - that's");
        Console.WriteLine("expected. Copy the full resulting address-bar URL and paste it below:");
        var pastedUrl = Console.ReadLine() ?? throw new InvalidOperationException("No callback URL provided.");

        var queryParams = ParseQueryString(new Uri(pastedUrl).Query);
        if (!queryParams.TryGetValue("code", out var code))
        {
            throw new InvalidOperationException("Callback URL carried no 'code' parameter.");
        }
        queryParams.TryGetValue("state", out var returnedState);
        if (!string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Callback 'state' did not match the value sent on the authorize request - rejecting as a possible CSRF/paste error.");
        }

        return await authClient.ExchangeAuthorizationCodeAsync(
            site, code, RedirectUri, clientId, pkce.CodeVerifier, clientSecret, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }
        return result;
    }
}
