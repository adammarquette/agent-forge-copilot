using System.Globalization;
using GauntletAI.AgentForge.LoadTestChat;

var baseUrl = Environment.GetEnvironmentVariable("LoadTest__BaseUrl")
    ?? "https://agent-forge-api-development.up.railway.app";
var durationSeconds = int.Parse(
    Environment.GetEnvironmentVariable("LoadTest__DurationSeconds") ?? "15", CultureInfo.InvariantCulture);
var concurrencyLevels = (Environment.GetEnvironmentVariable("LoadTest__ConcurrencyLevels") ?? "10")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(s => int.Parse(s, CultureInfo.InvariantCulture))
    .ToArray();

var cookiesRaw = Environment.GetEnvironmentVariable("LoadTest__SessionCookies") ?? throw new InvalidOperationException(
    "LoadTest__SessionCookies is required - a ';'-separated list of real 'Name=Value' session cookies " +
    "obtained via a genuine browser /launch login. See README.md.");
var sessionCookies = cookiesRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

Console.WriteLine($"Load-testing {baseUrl}/hubs/chat with {sessionCookies.Length} pooled session(s), {durationSeconds}s per concurrency level.");
Console.WriteLine("Every call is a real chat turn against real OpenEMR/LLM dependencies - this spends real money.");

foreach (var concurrency in concurrencyLevels)
{
    Console.WriteLine();
    Console.WriteLine($"=== Concurrency {concurrency} ===");
    var result = await LoadTestRunner.RunAsync(baseUrl, sessionCookies, concurrency, TimeSpan.FromSeconds(durationSeconds));
    result.Print();
}
