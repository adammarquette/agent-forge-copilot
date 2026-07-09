using System.Net.Http.Json;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using Refit;

namespace GauntletAI.AgentForge.SeedDemoPatients;

/// <summary>
/// Gets a real user-role OpenEMR access token for this one-time seeding run, via a genuine
/// interactive <c>authorization_code</c> + PKCE login (GitLab issue #26) - <c>client_credentials</c>
/// grants are hard-coded server-side to OpenEMR's system role, which cannot write any resource on
/// this fork (confirmed while investigating issue #22).
/// </summary>
public static class AuthBootstrap
{
    private const string RedirectUri = "https://sidecar.invalid/callback";
    private const string Scope = "openid api:fhir user/Patient.write user/Patient.read";

    public static async Task<string> AcquireAccessTokenAsync(string baseUrl, string site, CancellationToken cancellationToken = default)
    {
        var envToken = Environment.GetEnvironmentVariable("SeedDemo__AccessToken");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            Console.WriteLine("Using SeedDemo__AccessToken from the environment - skipping login.");
            return envToken;
        }

        using var http = new HttpClient();

        var (clientId, clientSecret) = await ResolveClientAsync(http, baseUrl, site, cancellationToken).ConfigureAwait(false);

        var pkce = PkceGenerator.Generate();
        var state = Guid.NewGuid().ToString("N");
        var authorizeUrl = AuthorizeUrlBuilder.Build(new AuthorizeRequest(
            AuthorizeEndpoint: $"{baseUrl.TrimEnd('/')}/oauth2/{site}/authorize",
            ClientId: clientId,
            RedirectUri: RedirectUri,
            Scopes: Scope.Split(' '),
            State: state,
            Pkce: pkce,
            Aud: $"{baseUrl.TrimEnd('/')}/apis/{site}/fhir"));

        Console.WriteLine();
        Console.WriteLine("Open this URL, log in as an admin/clinician user, and approve:");
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

        var authHttpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var authApi = RestService.For<IOpenEmrAuthApi>(authHttpClient);
        var authClient = new OpenEmrAuthClient(authApi);
        var token = await authClient.ExchangeAuthorizationCodeAsync(
            site, code, RedirectUri, clientId, pkce.CodeVerifier, clientSecret, cancellationToken).ConfigureAwait(false);

        return token.AccessToken;
    }

    /// <summary>
    /// Registers a fresh confidential client (or reuses <c>SeedDemo__ClientId</c>/<c>ClientSecret</c>
    /// from the environment if set). Registered clients land disabled - the operator must click
    /// Enable (Admin -&gt; System -&gt; API Clients) before the login step below will work.
    /// </summary>
    private static async Task<(string ClientId, string? ClientSecret)> ResolveClientAsync(
        HttpClient http, string baseUrl, string site, CancellationToken cancellationToken)
    {
        var envClientId = Environment.GetEnvironmentVariable("SeedDemo__ClientId");
        if (!string.IsNullOrWhiteSpace(envClientId))
        {
            return (envClientId, Environment.GetEnvironmentVariable("SeedDemo__ClientSecret"));
        }

        var registrationBody = new
        {
            client_name = "AgentForge Demo Patient Seeder",
            redirect_uris = new[] { RedirectUri },
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "client_secret_post",
            application_type = "private",
            scope = Scope,
        };

        using var response = await http.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/oauth2/{site}/registration", registrationBody, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Client registration failed: {(int)response.StatusCode} {response.StatusCode} - {responseBody}");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        var clientId = root.GetProperty("client_id").GetString()!;
        var clientSecret = root.TryGetProperty("client_secret", out var secretProp) ? secretProp.GetString() : null;

        Console.WriteLine($"Registered client '{clientId}'.");
        Console.WriteLine("Open Admin -> System -> API Clients, find 'AgentForge Demo Patient Seeder', click Enable, then press Enter.");
        Console.ReadLine();

        return (clientId, clientSecret);
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
