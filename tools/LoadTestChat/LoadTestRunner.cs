using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.SignalR.Client;

namespace GauntletAI.AgentForge.LoadTestChat;

/// <summary>Outcome of a single real chat-turn call (<c>RequestBrief</c>).</summary>
public sealed record CallResult(bool Success, double LatencyMs, string? Error);

/// <summary>Aggregated results for one concurrency level - Epic 12's NFR-PERF-4 deliverable shape.</summary>
public sealed record LoadTestResult(int Concurrency, TimeSpan Duration, IReadOnlyList<CallResult> Calls)
{
    public void Print()
    {
        var successLatencies = Calls.Where(c => c.Success).Select(c => c.LatencyMs).Order().ToArray();
        var errorCount = Calls.Count(c => !c.Success);
        var total = Calls.Count;

        Console.WriteLine($"Total calls: {total}, Successes: {successLatencies.Length}, Errors: {errorCount}");
        if (total > 0)
        {
            Console.WriteLine($"Error rate: {(double)errorCount / total:P2}");
        }

        if (successLatencies.Length > 0)
        {
            Console.WriteLine($"p50: {Percentile(successLatencies, 0.50):F0} ms");
            Console.WriteLine($"p95: {Percentile(successLatencies, 0.95):F0} ms");
            Console.WriteLine($"p99: {Percentile(successLatencies, 0.99):F0} ms");
        }

        foreach (var sample in Calls.Where(c => !c.Success).Select(c => c.Error).Distinct().Take(3))
        {
            Console.WriteLine($"  sample error: {sample}");
        }
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sortedValues.Length) - 1, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }
}

/// <summary>
/// Drives concurrent, real SignalR chat turns (<c>ChatHub.RequestBrief</c>) against a real deployed
/// AgentForge instance, multiplexing a small pool of real session cookies (obtained via a genuine
/// browser <c>/launch</c> login - see README.md) across many concurrent connections. This is Epic
/// 12's load-test harness (PRD.md NFR-PERF-3/4): every call is a real chat turn against real
/// OpenEMR/LLM dependencies, nothing here is mocked or synthetic.
/// </summary>
public static class LoadTestRunner
{
    public static async Task<LoadTestResult> RunAsync(
        string baseUrl, IReadOnlyList<string> sessionCookies, int concurrency, TimeSpan duration)
    {
        var results = new ConcurrentBag<CallResult>();
        using var cts = new CancellationTokenSource(duration);

        var workers = Enumerable.Range(0, concurrency)
            .Select(i => RunWorkerAsync(baseUrl, sessionCookies[i % sessionCookies.Count], results, cts.Token))
            .ToArray();

        await Task.WhenAll(workers).ConfigureAwait(false);

        return new LoadTestResult(concurrency, duration, [.. results]);
    }

    private static async Task RunWorkerAsync(
        string baseUrl, string cookie, ConcurrentBag<CallResult> results, CancellationToken cancellationToken)
    {
        var hubUri = new Uri($"{baseUrl.TrimEnd('/')}/hubs/chat");
        var cookieContainer = new CookieContainer();
        var (name, value) = ParseCookie(cookie);
        cookieContainer.Add(hubUri, new Cookie(name, value));

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUri.ToString(), options => options.Cookies = cookieContainer)
            .Build();

        try
        {
            await connection.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Duration elapsed before the connection even finished starting - a graceful stop, not a failure.
            return;
        }
        catch (Exception ex)
        {
            results.Add(new CallResult(false, 0, $"connect failed: {ex.Message}"));
            return;
        }

        var stopwatch = new Stopwatch();
        while (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Restart();
            try
            {
                await connection.InvokeAsync("RequestBrief", cancellationToken).ConfigureAwait(false);
                results.Add(new CallResult(true, stopwatch.Elapsed.TotalMilliseconds, null));
            }
            catch (OperationCanceledException)
            {
                // TaskCanceledException derives from OperationCanceledException - this fires when the
                // duration elapses mid-call. That's the runner's normal stop signal, not a failed call.
                break;
            }
            catch (Exception ex)
            {
                results.Add(new CallResult(false, stopwatch.Elapsed.TotalMilliseconds, ex.Message));
            }
        }
    }

    private static (string Name, string Value) ParseCookie(string raw)
    {
        var parts = raw.Split('=', 2);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Malformed session cookie '{raw}' - expected 'Name=Value'.");
        }

        return (parts[0].Trim(), parts[1].Trim());
    }
}
