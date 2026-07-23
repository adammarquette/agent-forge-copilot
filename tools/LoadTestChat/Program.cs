using System.Globalization;
using MarqSpec.AgentForge.LoadTestChat;

// The sidecar's own public domain was retired; it is reached only through the same-origin reverse-proxy
// front door under the /agentforge PathBase (reverse-proxy/nginx.conf.template). reference: gitlab#62
var baseUrl = Environment.GetEnvironmentVariable("LoadTest__BaseUrl")
    ?? "https://agent-forge-reverse-proxy-staging.up.railway.app/agentforge";
var durationSeconds = int.Parse(
    Environment.GetEnvironmentVariable("LoadTest__DurationSeconds") ?? "15", CultureInfo.InvariantCulture);
var concurrencyLevels = (Environment.GetEnvironmentVariable("LoadTest__ConcurrencyLevels") ?? "10")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(s => int.Parse(s, CultureInfo.InvariantCulture))
    .ToArray();

// Null/unset => Week-1 RequestBrief baseline (unchanged). Set => Week-2 evidence turn via AskFollowUp, so the
// one harness produces the vs-Week-1 comparison the cost/latency report needs. reference: gitlab#86
var question = Environment.GetEnvironmentVariable("LoadTest__Question");

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
            // Current seeded cohort (the earlier ids were retired by a reseed); scrape the /smart/patient-select
            // page with LoadTest__Debug=1 if these ever stop matching. For /evidence/ask the patient is only the
            // session's auth anchor - the flow retrieves from the guideline corpus, not the patient's FHIR record.
            ?? "a23a7ed4-54de-4dc7-b5c4-93d3e1d03fd4,a23a7ed5-867e-4ab3-bd7d-25fcc5b24f66,a23a7ed6-a13f-4241-a626-86a1589f5be8")
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

var flowUnderTest = question is null
    ? "RequestBrief (Week-1 pre-visit brief, SignalR)"
    : "POST /evidence/ask (Week-2 evidence graph, hybrid RAG)";
Console.WriteLine($"Load-testing {baseUrl}/hubs/chat with {sessionCookies.Length} pooled session(s), {durationSeconds}s per concurrency level.");
Console.WriteLine($"Flow under test: {flowUnderTest}.");
if (question is not null)
{
    Console.WriteLine($"Evidence question: \"{question}\"");
}
Console.WriteLine("Every call is a real chat turn against real OpenEMR/LLM dependencies - this spends real money.");

foreach (var concurrency in concurrencyLevels)
{
    Console.WriteLine();
    Console.WriteLine($"=== Concurrency {concurrency} ===");
    var result = await LoadTestRunner.RunAsync(
        baseUrl, sessionCookies, concurrency, TimeSpan.FromSeconds(durationSeconds), question);
    result.Print();
}
