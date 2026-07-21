using System.Globalization;
using MarqSpec.AgentForge.LoadTestChat;

var baseUrl = Environment.GetEnvironmentVariable("LoadTest__BaseUrl")
    ?? "https://agent-forge-api-staging-staging.up.railway.app";
var durationSeconds = int.Parse(
    Environment.GetEnvironmentVariable("LoadTest__DurationSeconds") ?? "15", CultureInfo.InvariantCulture);
var concurrencyLevels = (Environment.GetEnvironmentVariable("LoadTest__ConcurrencyLevels") ?? "10")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(s => int.Parse(s, CultureInfo.InvariantCulture))
    .ToArray();

string[] sessionCookies;
var cookiesRaw = Environment.GetEnvironmentVariable("LoadTest__SessionCookies");
if (!string.IsNullOrWhiteSpace(cookiesRaw))
{
    sessionCookies = cookiesRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
else
{
    var loginUsername = Environment.GetEnvironmentVariable("LoadTest__LoginUsername") ?? throw new InvalidOperationException(
        "Set LoadTest__SessionCookies directly, or LoadTest__LoginUsername/LoadTest__LoginPassword to " +
        "auto-bootstrap real sessions via Playwright. See README.md.");
    var loginPassword = Environment.GetEnvironmentVariable("LoadTest__LoginPassword")
        ?? throw new InvalidOperationException("LoadTest__LoginPassword is required alongside LoadTest__LoginUsername.");
    var patientIds = (Environment.GetEnvironmentVariable("LoadTest__PatientIds")
            ?? "a2362388-35e0-43de-97dc-450bd53e624e,a2382918-ebb2-46df-ba28-09dee162c67c,a2382cf3-3190-4ec5-94c8-4b507d4d08be")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.WriteLine($"No LoadTest__SessionCookies set - bootstrapping {patientIds.Length} real session(s) via Playwright...");
    var bootstrapped = new List<string>();
    foreach (var patientId in patientIds)
    {
        var cookie = await SessionBootstrap.AcquireSessionCookieAsync(baseUrl, loginUsername, loginPassword, patientId);
        Console.WriteLine($"  bootstrapped session for patient {patientId}");
        bootstrapped.Add(cookie);
    }

    sessionCookies = [.. bootstrapped];
}

Console.WriteLine($"Load-testing {baseUrl}/hubs/chat with {sessionCookies.Length} pooled session(s), {durationSeconds}s per concurrency level.");
Console.WriteLine("Every call is a real chat turn against real OpenEMR/LLM dependencies - this spends real money.");

foreach (var concurrency in concurrencyLevels)
{
    Console.WriteLine();
    Console.WriteLine($"=== Concurrency {concurrency} ===");
    var result = await LoadTestRunner.RunAsync(baseUrl, sessionCookies, concurrency, TimeSpan.FromSeconds(durationSeconds));
    result.Print();
}
