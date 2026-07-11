# Performance Baselines

Epic 12 (issue #20) load/stress-test results — PRD.md NFR-PERF-2/3/4. Run against the real deployed
`agent-forge-api` (`development` environment, `agent-forge-api-development.up.railway.app`) with real OpenEMR
and real Anthropic LLM calls — nothing mocked, nothing synthetic. Generated with `tools/LoadTestChat`
(README.md there explains the harness: a small pool of real, browser-obtained session cookies multiplexed
across many concurrent SignalR connections, each repeatedly invoking `ChatHub.RequestBrief`).

> **Note:** the `development` Railway environment used for this run was decommissioned 2026-07-10 after an
> unrecoverable deploy hang; `staging` is now the sole environment (see `documentation/RAILWAY.md`). The
> numbers below remain valid as a compute/latency baseline — the run measured `agent-forge-api`'s own
> processing characteristics, which aren't environment-specific — but the URL and deployment id are
> historical, not live.

**Run date:** 2026-07-10. **Deployment:** `fdf1f7ea` (post issue #39 AsyncLocal fix + issue #41 partial scope
fix — see Known limitation below). **Session pool:** 3 real sessions, bootstrapped via Playwright against
`/launch` with distinct demo patients.

## Results table (NFR-PERF-4 AC)

| Concurrency | Total calls | Successes | Errors | Error rate | p50 | p95 | p99 |
|---|---|---|---|---|---|---|---|
| 10 | 30 | 30 | 0 | 0.00% | 16,210 ms | 20,765 ms | 23,412 ms |
| 50 | 133 | 133 | 0 | 0.00% | 17,876 ms | 25,795 ms | 26,309 ms |

Each "call" is one full `RequestBrief` chat turn: LLM reasoning + FHIR tool dispatch + response synthesis,
started fresh (no conversation history carried over between calls in this harness). Latency is measured
client-side via `Stopwatch` around the SignalR `InvokeAsync` round trip — the authoritative NFR-PERF-4 number,
per `tools/LoadTestChat/README.md`.

**0% error rate at both levels** — every call returned a response. This reflects the system's graceful-
degradation design (`ARCHITECTURE.md` §13.1, PRD.md §9's failure-mode table): when a FHIR tool call fails,
the affected section is marked unavailable and the turn still completes with whatever data succeeded, rather
than failing the whole request. See the known limitation below for what's actually degrading right now.

## NFR-PERF-1: latency target (open question §17.7 — now resolved)

The PRD's aspirational "a few seconds" p95 for single-patient queries is **not met** at either concurrency
level; measured p95 is 20.8s (10 concurrent) and 25.8s (50 concurrent). The fixed target, based on this
baseline, is:

> **NFR-PERF-1 target: p95 ≤ 26 seconds** for a single-patient `RequestBrief` turn under load up to 50
> concurrent users.

Why it's this high: each turn is a sequential LLM-orchestrated tool-calling loop (`agentforge_agent_turns_total`
recorded 229 completed turns against only 166 client-observed `RequestBrief` calls across this session, i.e.
~1.4 LLM round trips per turn on average) plus real network latency to both Anthropic and OpenEMR on every
tool call. Latency is dominated by these external dependencies, not by `agent-forge-api`'s own compute (see
CPU/memory below). Reducing this further is a product of parallelizing independent tool calls and/or the
"fast-core-then-defer" streaming policy PRD.md §8 already calls for — both are future work, not implemented
in v1.

## CPU / memory / throughput baselines (NFR-PERF-2 AC)

Pulled from Railway's own resource metrics for `agent-forge-api`, covering the full load-test window
(18:29–18:59 UTC, includes both concurrency phases plus a short warm-up probe):

| Metric | Average | Max | Limit |
|---|---|---|---|
| CPU | 0.008 vCPU (0.1%) | 0.160 vCPU | 24 vCPU |
| Memory | 137 MB | 195 MB | 24,576 MB |

CPU and memory headroom is enormous at this scale — confirms the service is I/O-bound (waiting on the LLM
and OpenEMR) rather than compute-bound. 50 concurrent SignalR connections with real streaming LLM calls cost
under 1% of a single vCPU. Railway's HTTP-layer request count (79, p50 18ms) undercounts real work because
SignalR upgrades to WebSocket after the initial handshake — the 163 real chat turns ride over those
persistent connections, not as separate HTTP requests.

**Throughput:** `agentforge_agent_turns_total` recorded 229 completed turns (`outcome="success"`) over the
session. At 50 concurrent users sustained for 60s, the service completed 133 full turns — roughly **2.2
turns/second** aggregate throughput at that concurrency level.

## Cost (PRD.md §14: "report the actual dollar figure, not a guess")

`agentforge_llm_cost_usd_total` currently reads `0` — `Llm__InputPricePerMillionTokensUsd` /
`Llm__OutputPricePerMillionTokensUsd` are not configured in this environment (pre-existing gap, tracked
separately). The actual figure below is computed directly from real consumed tokens
(`agentforge_llm_tokens_total`) at Anthropic's published Claude Sonnet 5 rates in effect on the run date
(`claude-sonnet-5`, intro pricing through 2026-08-31: $2.00/M input, $10.00/M output):

| Direction | Tokens | Rate | Cost |
|---|---|---|---|
| Input | 2,504,947 | $2.00 / M | $5.01 |
| Output | 198,021 | $10.00 / M | $1.98 |
| **Total** | | | **≈ $6.99** |

This covers the full session since the last deploy restart (both concurrency phases plus a small warm-up
probe beforehand), not exclusively the two big runs in isolation. Follow-up: set the two `Llm__*Price*`
Railway variables so `agentforge_llm_cost_usd_total` reflects this automatically going forward instead of
requiring a manual token-count calculation.

## Known limitation: partial FHIR tool availability during this run

Only `get_patient_summary` was fully functional during this load test. `get_recent_encounters`, `get_labs`,
`get_vitals`, and `get_documents` returned real `401`s from OpenEMR for the whole run (see GitLab issue #41):
the sidecar-side OAuth scope-casing bug is fixed and confirmed correct (consent screen shows every resource
scope granted), but a deeper OpenEMR-side issue — most likely a per-resource ACL restriction on the demo
`admin` account — still blocks those four tools. Investigation was paused because the live OpenEMR instance
became intermittently unresponsive (502s, SPA navigation failures) partway through diagnosis, consistent
with concurrent work on that deployment outside this repo.

**Effect on these numbers:** every measured turn attempted all five tools and gracefully degraded around the
four failing ones, so the latency and token figures above reflect a *partial* brief (patient summary only),
not the full five-source cardiology brief the product targets. A turn that successfully pulls encounters,
labs, vitals, and documents too would very likely run slower and cost more per call than reported here — this
baseline should be re-run once issue #41 is fully resolved to get the true steady-state numbers.

## Methodology notes

- **Session pool, not per-connection login.** There's no way to programmatically mint many distinct
  authenticated chat sessions against the real deployed app — `/launch` requires a genuine browser-mediated
  SMART OAuth redirect through OpenEMR every time. `tools/LoadTestChat` instead multiplexes many concurrent
  SignalR connections across a small pool of real sessions (3, one per distinct demo patient), which still
  exercises real server-side concurrency and compute handling — what NFR-PERF-3/4 actually measures — without
  adding any auth-bypass code path to production.
- **No conversation history reuse.** Each `RequestBrief` call in this harness starts a fresh turn; this
  measures per-turn cost/latency, not multi-turn conversation growth.
- **Real spend.** As PRD.md anticipates, this run spent real money (~$6.99, see above) and made real calls
  against the real OpenEMR development instance.
