using Microsoft.Playwright;

namespace GauntletAI.AgentForge.LoadTestChat;

/// <summary>
/// Automates obtaining a real chat session cookie for load testing (GitLab issue #20, following up
/// on #30's Playwright login automation). Drives the deployed sidecar's own real
/// <c>/launch</c> -&gt; OpenEMR login/consent -&gt; <c>/callback</c> flow, then reads the resulting
/// session cookie straight out of the browser context. Unlike
/// <c>PlaywrightLoginAutomation</c> (which registers a throwaway OAuth client and talks to OpenEMR's
/// <c>/authorize</c> directly), this hits the sidecar's own <c>/launch</c> so the real, deployed
/// <c>OpenEmr__ClientId</c> is used and the redirect actually resolves to a real page - no network
/// interception needed, the browser just completes the navigation like a real user's would.
/// </summary>
public static class SessionBootstrap
{
    public static async Task<string> AcquireSessionCookieAsync(
        string baseUrl, string username, string password, string patientId, CancellationToken cancellationToken = default)
    {
        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
            .ConfigureAwait(false);
        var context = await browser.NewContextAsync().ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);

        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/launch", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle })
            .ConfigureAwait(false);

        if (Environment.GetEnvironmentVariable("LoadTest__Debug") == "1")
        {
            Console.WriteLine($"[debug] after /launch, landed on: {page.Url}");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-after-launch.png", FullPage = true }).ConfigureAwait(false);
            await File.WriteAllTextAsync("debug-after-launch.html", await page.ContentAsync(), cancellationToken).ConfigureAwait(false);
        }

        await page.FillAsync("input[name=username]", username).ConfigureAwait(false);
        await page.FillAsync("input[name=password]", password).ConfigureAwait(false);
        await page.ClickAsync("button[name=user_role][value=api]").ConfigureAwait(false);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 })
            .ConfigureAwait(false);

        if (Environment.GetEnvironmentVariable("LoadTest__Debug") == "1")
        {
            Console.WriteLine($"[debug] after login submit, landed on: {page.Url}");
            await File.WriteAllTextAsync("debug-after-login.html", await page.ContentAsync(), cancellationToken).ConfigureAwait(false);
        }

        var patientButtonSelector = $"button[data-patient-id='{patientId}']";
        var patientButton = await page.WaitForSelectorAsync(patientButtonSelector, new PageWaitForSelectorOptions { Timeout = 15000 })
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Session bootstrap: no patient-select row found for patient id '{patientId}' - the login " +
                "may have failed, or this id no longer exists.");
        await patientButton.ClickAsync().ConfigureAwait(false);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 })
            .ConfigureAwait(false);

        // Consent -> real redirect through /callback -> the sidecar's own redirect to the chat SPA.
        // No network interception needed here (unlike PlaywrightLoginAutomation): this redirect_uri
        // is the real deployed sidecar, so the browser actually completes the navigation.
        var debug = Environment.GetEnvironmentVariable("LoadTest__Debug") == "1";
        IResponse? callbackResponse = null;
        if (debug)
        {
            page.Response += (_, response) =>
            {
                if (response.Url.Contains("/callback", StringComparison.Ordinal))
                {
                    callbackResponse = response;
                }
            };
        }

        if (debug)
        {
            await File.WriteAllTextAsync("debug-consent-page.html", await page.ContentAsync(), cancellationToken).ConfigureAwait(false);
        }

        await page.ClickAsync("button[name=proceed]").ConfigureAwait(false);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 })
            .ConfigureAwait(false);

        if (debug)
        {
            Console.WriteLine($"[debug] after consent, landed on: {page.Url}");
            if (callbackResponse is not null)
            {
                var headers = await callbackResponse.AllHeadersAsync().ConfigureAwait(false);
                var location = headers.TryGetValue("location", out var loc) ? loc : "(none)";
                string body;
                try
                {
                    body = await callbackResponse.TextAsync().ConfigureAwait(false);
                }
                catch (PlaywrightException ex)
                {
                    body = $"(unavailable: {ex.Message})";
                }

                Console.WriteLine($"[debug] /callback response: status={callbackResponse.Status} location={location}");
                Console.WriteLine($"[debug] /callback body: {body}");
            }
            else
            {
                Console.WriteLine("[debug] /callback response: no matching response captured");
            }

            await page.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-after-consent.png", FullPage = true }).ConfigureAwait(false);
            await File.WriteAllTextAsync("debug-after-consent.html", await page.ContentAsync(), cancellationToken).ConfigureAwait(false);
        }

        var cookies = await context.CookiesAsync().ConfigureAwait(false);
        var sessionCookie = cookies.FirstOrDefault(c => c.Name == ".AspNetCore.Session")
            ?? throw new InvalidOperationException(
                "Session bootstrap: no .AspNetCore.Session cookie found in the browser context after login - " +
                "the launch may not have completed successfully.");

        if (debug)
        {
            Console.WriteLine(
                $"[debug] session cookie attrs: domain={sessionCookie.Domain} path={sessionCookie.Path} " +
                $"secure={sessionCookie.Secure} httpOnly={sessionCookie.HttpOnly} sameSite={sessionCookie.SameSite}");
        }

        return $"{sessionCookie.Name}={sessionCookie.Value}";
    }
}
