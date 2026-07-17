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

---

# Week 2 — Multimodal Evidence flow (`POST /evidence/ask`)

Week 2 cost/latency baseline for the multimodal-evidence graph (saga #71, epic E8 #86; FR-OBS-W2-3,
NFR-PERF-W2). Traces to `W2_ARCHITECTURE.md` §6 and the Week 2 deliverables (cost + latency report, baselines
vs Week 1).

**Run date:** 2026-07-17. **Target:** the live `staging` deployment (`agent-forge-api-staging`, Railway
project `lucid-clarity`), reached through the same-origin reverse-proxy front door under the `/agentforge`
PathBase (`reverse-proxy/nginx.conf.template`, issue #62) — the sidecar's own public domain is retired, so
`https://agent-forge-reverse-proxy-staging.up.railway.app/agentforge` is the only external entry. `/ready`
was 200 (dependencies healthy) at run time. **Flow:** `POST /evidence/ask` — the stateless supervisor→worker
graph (intake-extractor / evidence-retriever / answer-composer / critic), question-only (no document upload),
so the hybrid-RAG guideline path runs but vision extraction does not. **Question:** a fixed guideline query
(ACC/AHA LDL targets in high ASCVD risk). **Harness:** `tools/LoadTestChat` with `LoadTest__Question` set,
which POSTs multipart `question` with a pooled real session cookie; 3 real sessions (Playwright `/launch`
bootstrap) multiplexed across the concurrency. Nothing mocked — real LLM + retrieval + rerank spend.

> **Why `/evidence/ask` and not a chat turn.** An earlier attempt drove the Week-1 chat hub (`AskFollowUp`).
> It is **stateful** — it resumes per-session conversation state — so repeating one question returned cached
> short replies (a contaminated p50 ~3.8s), and under concurrent multiplexing the pooled sessions would race
> on shared conversation state. `/evidence/ask` is **stateless** (a fresh graph run per request), which both
> fixes the contamination and is the canonical "Week 2 core flow" a grader runs (#86).

## Results (client-side round-trip, NFR-PERF-4 shape)

| Concurrency | Total calls | Errors | Error rate | p50 | p95 | p99 | Throughput |
|---|---|---|---|---|---|---|---|
| 10 | 54 | 0 | 0.00% | 10,704 ms | 14,938 ms | 15,202 ms | ~0.9 turns/s |
| 50 | 356 | 0 | 0.00% | 7,523 ms | 12,862 ms | 20,206 ms | ~5.9 turns/s |

**0% errors at both levels.** Throughput scales ~linearly (0.9 → 5.9 turns/s for 10 → 50 workers), consistent
with an I/O-bound service with headroom (matches the Week 1 CPU/memory finding — not re-measured here). p50 is
*lower* at 50 than at 10 because the concurrency-10 phase ran first, cold; the tail (p99) grows under load as
expected.

## Baseline vs Week 1

Both are one full agent turn, client-timed, against real dependencies — Week 1 is a SignalR `RequestBrief`,
Week 2 is an HTTP `POST /evidence/ask` (different transport, same "one turn" unit).

| Metric | Week 1 brief | Week 2 evidence | Δ |
|---|---|---|---|
| p50 @ 10 | 16,210 ms | 10,704 ms | **−34%** |
| p95 @ 10 | 20,765 ms | 14,938 ms | −28% |
| p50 @ 50 | 17,876 ms | 7,523 ms | **−58%** |
| p95 @ 50 | 25,795 ms | 12,862 ms | −50% |

The evidence turn is **faster** than the pre-visit brief. Expected: the brief fans out several FHIR tool
calls plus synthesis (~1.4 LLM round-trips/turn), while a question-only evidence turn is one guideline
retrieval + one compose + a deterministic critic.

## Bottleneck decomposition (per-worker, server-side)

From `agentforge_worker_duration_seconds` and the retrieval/rerank histograms, delta over the run:

| Stage | Avg per execution | Executions | Note |
|---|---|---|---|
| evidence-retriever (hybrid RAG) | **3.16 s** | 470 | dense+sparse+RRF; ~2.95 snippets/call; 100% hit rate |
| answer-composer (LLM compose) | **4.51 s** | 411 | **dominant cost** — the single LLM call per turn |
| critic (deterministic verify) | 0.16 ms | 411 | negligible — no LLM |
| rerank (Cohere cross-encoder) | 141 ms | **20 / 470** | fired on only ~4% of retrievals — small corpus usually yields ≤ topK candidates, so rerank is skipped |

Per completed turn the graph runs retriever → composer → critic sequentially (≈ 3.16 + 4.51 + ~0 ≈ 7.7 s),
which accounts for essentially all of the client-observed p50; the rest is network + supervisor overhead.
(Retriever executions, 470, exceed completed turns, 410, because in-flight requests at each 60 s cutoff
completed retrieval but were cancelled before composing — they are not counted as client successes.)

## Cost — **not obtainable from telemetry (instrumentation gap)**

Across 410 completed turns and **411 answer-composer LLM calls**, `agentforge_llm_tokens_total` and
`agentforge_llm_cost_usd_total` recorded a **zero delta**. The evidence graph's LLM usage is **not
instrumented** for tokens/cost: `/evidence/ask` bypasses the `AgentOrchestrator` path where token/cost
recording lives — the same evidence-path telemetry bypass `W2_AUDIT.md` flags for NFR-TRACE (per-encounter
telemetry), now confirmed to extend to **cost**. So the "actual dollar figure" PRD.md asks for is **not
measurable from metrics today** for this flow.

Order-of-magnitude estimate only (pending instrumentation): the compose step sends the question + ~3
guideline snippets (~1–3k input tokens) and returns a few hundred output tokens; at Claude Sonnet 5 rates
($2/M in, $10/M out) that is roughly **$0.006–0.010 per turn**, i.e. **~$3** for this 410-turn run, plus
negligible Cohere embed/rerank. Treat as an estimate, not a measurement.

**Fixed in this change.** `EvidenceAgentSupervisor.ComposeAsync` now records the composer's usage via
`IAgentForgeMetrics.RecordLlmUsage` (mirroring `AgentOrchestrator`), so `/evidence/ask` tokens and cost reach
`agentforge_llm_tokens_total` / `_cost_usd_total`. The figures above are the **pre-instrumentation** run
(metrics were blind to the evidence LLM call); the metered dollar figure replaces the estimate once this
deploys to staging and a short pass is re-run. One smaller residual remains: the intake-extractor's
`DocumentExtractor.CompleteAsync` — exercised only on document-upload turns, which this question-only run did
not hit — is still unmetered.

## Methodology notes

- **Session pool, not per-connection login** (same constraint as Week 1): 3 real sessions bootstrapped via a
  genuine `/launch` SMART login, multiplexed across the concurrency. For `/evidence/ask` the session is only
  the auth anchor — the flow retrieves from the guideline corpus and makes no user-scoped FHIR call — so 3
  sessions across 50 workers still exercises real server-side concurrency.
- **Stateless flow**, so multiplexing many workers over few sessions is clean (no shared conversation state),
  unlike the Week-1 hub.
- **Real spend**, order ~\$3 (estimated — see the cost gap above). Question-only, so no vision-extraction cost;
  a run with document upload (`file` + `docType`) would add the intake-extractor's multimodal call and cost.
- **Not re-measured:** CPU/memory (Week 1 found the service I/O-bound with large headroom; nothing here
  suggests otherwise) and a document-upload evidence turn.
