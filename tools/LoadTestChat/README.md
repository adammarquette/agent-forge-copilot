# LoadTestChat

Load/stress-test harness for the deployed AgentForge sidecar (GitLab issue #20, Epic 12 - PRD.md
NFR-PERF-3/4). Not part of the shipped product and not run by CI - `dotnet build`/`dotnet format` cover it,
but `unit-tests`/`integration-tests` filter by project filename and never invoke it.

## Why session cookies, not a scripted login

There is no way to programmatically mint many distinct authenticated chat sessions: `/launch` on the real
deployed app requires a genuine browser-mediated SMART OAuth redirect through OpenEMR every time - the
test-only session-seed bypass used by the integration test suite only exists in the in-process test host,
never in the real deployed binary (by design - it's an auth bypass, and it must never exist in a real
deployment).

Instead, this tool multiplexes many concurrent SignalR connections across a **small pool of real session
cookies** you obtain once via an actual browser login. This tests server-side concurrency/compute handling
under load - what NFR-PERF-3/4 measures - without touching production auth surface at all.

## Getting a session cookie

1. Open `https://agent-forge-api-staging-staging.up.railway.app/launch` in a real browser.
2. Log in and approve as usual - you'll land on the chat SPA once the session is established.
3. Open DevTools → Application (Chrome) / Storage (Firefox) → Cookies, find the session cookie (default
   ASP.NET Core session cookie name: `.AspNetCore.Session`), and copy its value.
4. Repeat 1-3 a couple more times (in separate browser profiles/incognito windows, so each login is a
   genuinely distinct session) if you want a bigger pool for the real run - one is enough for the dry run.

## Running it

```bash
LoadTest__SessionCookies=".AspNetCore.Session=<value1>;.AspNetCore.Session=<value2>" \
LoadTest__ConcurrencyLevels="10,50" \
LoadTest__DurationSeconds="60" \
dotnet run --project tools/LoadTestChat
```

Every call is a real `RequestBrief` chat turn against real OpenEMR/LLM dependencies - **this spends real
money** (PRD.md explicitly expects and asks for the actual dollar figure to be reported, pulled from the
`agentforge_llm_cost_usd_total` metric). Start with a short, low-concurrency dry run to confirm the harness
actually works before running the full 10/50-concurrent-user pass.

### Measuring the Week-2 evidence flow

Set `LoadTest__Question` to a guideline/evidence question and every call becomes an `AskFollowUp` turn that
drives the hybrid-RAG `retrieve_evidence` path (Core Req 3) instead of the Week-1 brief. Both flows share the
same client-side round-trip measurement, so the two runs are directly comparable — the vs-Week-1 baseline the
cost/latency report needs. reference: gitlab#86

```bash
LoadTest__LoginUsername=cardio1 LoadTest__LoginPassword='<demo password>' \
LoadTest__Question="What do the current cardiology guidelines recommend for this patient's LDL target given their ASCVD risk?" \
LoadTest__ConcurrencyLevels="10,50" LoadTest__DurationSeconds="60" \
dotnet run --project tools/LoadTestChat
```

### Environment variables

| Variable | Purpose |
|---|---|
| `LoadTest__SessionCookies` | **Required unless the login vars below are set.** `;`-separated `Name=Value` session cookies from real browser logins |
| `LoadTest__BaseUrl` | Deployed base URL (defaults to the staging Railway instance) |
| `LoadTest__ConcurrencyLevels` | `,`-separated concurrency levels to run in sequence (default `10`) |
| `LoadTest__DurationSeconds` | How long to hammer each concurrency level, in seconds (default `15`) |
| `LoadTest__Question` | If set, every call is an `AskFollowUp(question)` **evidence turn** (Week-2 hybrid-RAG path, Core Req 3) instead of a `RequestBrief` brief (Week-1). Use a guideline/evidence question so the agent invokes `retrieve_evidence`. |
| `LoadTest__LoginUsername` / `LoadTest__LoginPassword` | Alternative to `LoadTest__SessionCookies`: auto-bootstrap real sessions via Playwright (a genuine `/launch` SMART login). |
| `LoadTest__PatientIds` | `,`-separated patient ids for the Playwright bootstrap - one distinct session per id. |

## Output

For each concurrency level: total calls, successes/errors, error rate, and p50/p95/p99 latency in
milliseconds, computed client-side from real round-trip timings - the numbers NFR-PERF-4 asks for. Cross-
reference against the deployed app's own `/metrics` (`agentforge_agent_turn_duration_seconds` histogram,
`agentforge_agent_turns_total`, `agentforge_llm_cost_usd_total`) and Railway's dashboard (CPU/memory) for
the full baseline picture.
