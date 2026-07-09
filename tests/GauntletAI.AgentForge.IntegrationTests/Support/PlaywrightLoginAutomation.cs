using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using Microsoft.Playwright;
using Refit;

namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>
/// Drives a real SMART standalone-launch login via Playwright (GitLab issue #30) - no human in the
/// loop. Confirmed live against the QA server: staff login ("OpenEMR Login") -&gt; patient-select
/// (matched by exact <c>data-patient-id</c>, never by name - the QA panel carries two "Ada
/// Testpatient" rows with different ids, which is exactly what caused a real token/patient mix-up
/// earlier) -&gt; consent (scopes come back pre-checked) -&gt; the redirect is captured via network
/// interception, since <see cref="RedirectUri"/> is a deliberately non-resolving host. Registering
/// with <c>application_type: "private"</c> - a field the production <see cref="ClientRegistrationRequest"/>
/// never sends - skips the "enable this client in Admin -&gt; System -&gt; API Clients" step every
/// other tool in this repo needs; confirmed by a real, successful token exchange without it.
/// </summary>
public static class PlaywrightLoginAutomation
{
    private const string RedirectUri = "https://sidecar.invalid/callback";

    /// <summary>
    /// Registers a throwaway client, drives the full login/consent flow for <paramref name="patientId"/>
    /// as <paramref name="username"/>/<paramref name="password"/>, and exchanges the resulting code for
    /// a real token via the production <see cref="IOpenEmrAuthClient"/> - the only step here that isn't
    /// QA-only raw HTTP, since a code exchange is already exactly production-shaped.
    /// </summary>
    public static async Task<TokenResponse> AcquireTokenAsync(
        string baseUrl,
        string site,
        string username,
        string password,
        string patientId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        var clientId = await RegisterPrivateClientAsync(http, baseUrl, site, scopes, cancellationToken).ConfigureAwait(false);

        var pkce = PkceGenerator.Generate();
        var state = Guid.NewGuid().ToString("N");
        var authorizeUrl = AuthorizeUrlBuilder.Build(new AuthorizeRequest(
            AuthorizeEndpoint: $"{baseUrl.TrimEnd('/')}/oauth2/{site}/authorize",
            ClientId: clientId,
            RedirectUri: RedirectUri,
            Scopes: scopes,
            State: state,
            Pkce: pkce,
            Aud: $"{baseUrl.TrimEnd('/')}/apis/{site}/fhir"));

        var (code, returnedState) = await DriveLoginAsync(authorizeUrl, username, password, patientId, cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Playwright login automation: callback 'state' did not match the pending authorize request - " +
                "rejecting as a possible CSRF/interception error.");
        }

        var authApi = RestService.For<IOpenEmrAuthApi>(new HttpClient { BaseAddress = new Uri(baseUrl) });
        var authClient = new OpenEmrAuthClient(authApi);
        return await authClient.ExchangeAuthorizationCodeAsync(
                site, code, RedirectUri, clientId, pkce.CodeVerifier, clientSecret: null, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> RegisterPrivateClientAsync(
        HttpClient http, string baseUrl, string site, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        var registrationBody = new
        {
            client_name = "AgentForge Playwright Login Automation (QA, throwaway)",
            redirect_uris = new[] { RedirectUri },
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "client_secret_post",
            application_type = "private",
            scope = string.Join(' ', scopes),
        };

        using var response = await http.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/oauth2/{site}/registration", registrationBody, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Client registration failed: {(int)response.StatusCode} {response.StatusCode} - {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("client_id").GetString()
            ?? throw new InvalidOperationException($"Registration response carried no client_id: {body}");
    }

    private static async Task<(string Code, string? State)> DriveLoginAsync(
        Uri authorizeUrl, string username, string password, string patientId, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
            .ConfigureAwait(false);
        var page = await browser.NewPageAsync().ConfigureAwait(false);

        await page.GotoAsync(authorizeUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle })
            .ConfigureAwait(false);

        await page.FillAsync("input[name=username]", username).ConfigureAwait(false);
        await page.FillAsync("input[name=password]", password).ConfigureAwait(false);
        await page.ClickAsync("button[name=user_role][value=api]").ConfigureAwait(false);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 })
            .ConfigureAwait(false);

        var patientButtonSelector = $"button[data-patient-id='{patientId}']";
        var patientButton = await page.WaitForSelectorAsync(patientButtonSelector, new PageWaitForSelectorOptions { Timeout = 15000 })
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Playwright login automation: no patient-select row found for patient id '{patientId}' - " +
                "the QA login may have failed, or this id no longer exists.");
        await patientButton.ClickAsync().ConfigureAwait(false);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 })
            .ConfigureAwait(false);

        var redirectRequestTask = page.WaitForRequestAsync(req => req.Url.StartsWith(RedirectUri, StringComparison.Ordinal));
        await page.ClickAsync("button[name=proceed]").ConfigureAwait(false);
        var redirectRequest = await redirectRequestTask.ConfigureAwait(false);

        var callbackUri = new Uri(redirectRequest.Url);
        var parameters = callbackUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty);

        if (!parameters.TryGetValue("code", out var code))
        {
            throw new InvalidOperationException(
                $"Playwright login automation: redirect carried no 'code' parameter: {redirectRequest.Url}");
        }

        parameters.TryGetValue("state", out var state);
        return (code, state);
    }
}
