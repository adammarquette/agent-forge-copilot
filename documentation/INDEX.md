# INDEX.md — the wiki's front door

**Read this first, then drill into the one doc or project you need.** This is the content-oriented index for
the AgentForge wiki (the pattern is Andrej Karpathy's
[LLM Wiki](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f), explained in the root
[`README.md`](../README.md#why-so-much-cross-referencing)). The `.slnx`/`.csproj` files already index *what
code exists*; this file indexes *what each thing is for* and — the edge those project files don't carry —
**which code project implements which requirement, use case, and concept.** Traverse from here instead of
re-deriving context each session.

Two traceability tables already live elsewhere and are **not** duplicated here:
- `PRD.md` defines the requirement IDs (`FR-*`, `NFR-*`).
- `USERS.md` §5 maps **capability → use case** (`UC-1..UC-6`).

This file adds the missing third leg: **doc / requirement / use-case → code project.**

---

## 1. Documents catalog (the sources)

| Doc | Owns / is authoritative for | ID namespace it defines |
|---|---|---|
| [`PRD.md`](PRD.md) | Problem statement, functional & non-functional requirements | `FR-*`, `NFR-*` |
| [`USERS.md`](USERS.md) | The one persona, the 90-second workflow, use cases + capability→UC map (§5) | `UC-1..UC-6` |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Topology, trust boundaries, verification design, deployment; the vMVP decision log | doc §-numbers |
| [`W2_ARCHITECTURE.md`](W2_ARCHITECTURE.md) | **(Week 2)** Multimodal Evidence Agent — ingestion, supervisor/worker graph, hybrid RAG, eval gate | `W2-D1..W2-D14` |
| [`INTERFACE_CONTROL.md`](INTERFACE_CONTROL.md) | The OpenEMR external interface (FHIR R4 / OAuth2 / SMART) — the ICD | ICD sections A, B, … |
| [`ENGINEERING_STANDARDS.md`](ENGINEERING_STANDARDS.md) | Stack, dependencies, testing tiers, logging/security standards, solution layout (§9) | `§`-numbers |
| [`AUDIT.md`](AUDIT.md) | Findings from auditing the OpenEMR fork (security / perf / data quality) | audit finding refs |
| [`W2_AUDIT.md`](W2_AUDIT.md) | Week 2 implementation audit — per-requirement Met/Partial/Gap coverage, risks, prioritized recommendations vs the submission gates | audit finding refs |
| [`PERFORMANCE_BASELINES.md`](PERFORMANCE_BASELINES.md) | Measured latency/throughput baselines behind the `NFR-PERF-*` budgets (Epic 12) | — |
| [`CI-SETUP.md`](CI-SETUP.md) · [`RAILWAY.md`](RAILWAY.md) | CI pipeline and Railway (staging) deployment operations | — |
| [`DEPLOYMENT_TOPOLOGY.md`](DEPLOYMENT_TOPOLOGY.md) | Physical/network view of the deployed `staging` environment — services, public vs private exposure, flows, trust boundaries (mermaid) | — |
| [`supporting/`](supporting/) | Source requirement PDFs (Week 1 & 2) and the architecture-defense decks | — |

---

## 2. Code map (the missing edge: concept → project → spec)

Layout is authoritative in [`ENGINEERING_STANDARDS.md` §9](ENGINEERING_STANDARDS.md). Each `src/` project also
carries the finer detail; this is the family-level jump table.

| Project (`src/MarqSpec.AgentForge.*`) | What it is | Implements | Specified by |
|---|---|---|---|
| `.Api` | ASP.NET Core host: BFF (server-side token custody), SignalR hub, `/health` + `/ready` | `FR-AUTH-*`, `FR-CHAT-*`, `NFR-HEALTH-1`, `NFR-SEC-*` | `ARCHITECTURE.md` (BFF/trust), `PRD.md` |
| `.Agent` | Orchestrator: multi-turn loop, tool chaining, prioritized synthesis | `FR-CHAT-*`, `FR-VERIF-0` (routing) | `ARCHITECTURE.md`, `USERS.md` (UC-1/2/6) |
| `.Mcp` | MCP tool server: strict tool contracts, audit log, read-only FHIR tools (min-necessary) | `FR-DATA-*`, `NFR-CONTRACT-1` | `INTERFACE_CONTROL.md`, `PRD.md` |
| `.Verification` | Source attribution + cardiology domain-constraint rules (INR/QT/renal/chronotropy) | `FR-VERIF-*` | `ARCHITECTURE.md` (verification), `USERS.md` (UC-3) |
| `.Integration.OpenEmr` | Refit clients, OAuth/SMART flow, FHIR mappers, Polly resilience | `FR-AUTH-*`, `FR-DATA-*`, `NFR-REL-*` | `INTERFACE_CONTROL.md` (ICD) |
| `.Llm` | `ILlmProvider` abstraction + one implementation (the provider seam) | `FR-CHAT-*` (generation) | `ARCHITECTURE.md` |
| `.Observability` | OpenTelemetry activity source + metrics, correlation IDs | `FR-OBS-*`, `NFR-TRACE-1`, `NFR-PERF-*` | `ENGINEERING_STANDARDS.md` §7, `PERFORMANCE_BASELINES.md` |
| `.Data` | **(Week 2)** EF Core + pgvector data layer: hybrid-RAG corpus + `DerivedFactStore` (entities, `DbContext`, migrations) | Week 2 persistence | `W2_ARCHITECTURE.md` §5 (W2-D14) |
| `.Documents` | **(Week 2)** VLM document extraction → strict schema; PdfPig word/bbox layer for citations | Week 2 ingestion (`FR-DOC-*`) | `W2_ARCHITECTURE.md` §3 |
| `.Retrieval` | **(Week 2)** Hybrid RAG: dense pgvector + sparse FTS + RRF fusion + Cohere rerank (`IDenseRetriever`/`IReranker`/`IEmbeddingProvider` seams) | Week 2 retrieval (`FR-RAG-*`) | `W2_ARCHITECTURE.md` §5 |
| `.Agents` | **(Week 2)** Evidence-agent supervisor + four workers (extract → retrieve → compose → critic) + ingestion service; logged/metered handoffs | Week 2 multi-agent graph (`FR-GRAPH-*`) | `W2_ARCHITECTURE.md` §6 |
| `tests/…UnitTests` · `…IntegrationTests` · `…EvalTests` · `…Evals` | Mocked unit tests (test-first) · real-dependency QA tests · deterministic rubric xUnit tests · golden-set eval runner (cases in top-level `evals/`) | `FR-EVAL-*` | `ENGINEERING_STANDARDS.md` §8, `tests/AGENTS.md` |

---

## 3. Use case → code (via capability)

Read `USERS.md` §4/§5 for the *why*; this is the jump from a use case to the projects that carry it.

| Use case | Primary projects |
|---|---|
| [UC-1](USERS.md) Pre-visit brief | `.Agent` (synthesis) + `.Mcp` (retrieval) + `.Verification` (citations) |
| [UC-2](USERS.md) Grounded multi-turn follow-up | `.Agent` (multi-turn + chaining) + `.Mcp` |
| [UC-3](USERS.md) Med reconciliation & safety | `.Verification` (rules) + `.Mcp` |
| [UC-4](USERS.md) Authorization-aware access | `.Api`/`.Agent` (enforcement below the model) + `.Mcp` (scoped token) |
| [UC-5](USERS.md) Graceful degradation | `.Agent`/`.Api` (partial-truth signaling) + `.Integration.OpenEmr` (resilience) |
| [UC-6](USERS.md) On-demand day's agenda | `.Agent` (per-patient roster, isolated sessions) + `.Mcp` |

---

## 4. Requirement families (quick reference — full text in `PRD.md`)

- **`FR-AUTH-1..4`** SMART EHR launch, OAuth2, server-side token custody
- **`FR-CHAT-1..4`** conversational, streaming, patient-scoped, multi-turn
- **`FR-DATA-1..4`** read-only FHIR, minimum-necessary scoping
- **`FR-VERIF-0..4`** two-layer verification (source attribution + cardiology constraints)
- **`FR-OBS-1..4`** observability (traces, metrics, logs, audit) — logs aggregated in a self-hosted Loki (Epic 107)
- **`FR-EVAL-1..4`** evaluation suite
- **`NFR-*`** `CONTRACT-1`, `PERF-1..4`, `REL-1/2`, `SCALE-1`, `SEC-1/2`, `TRACE-1`, `HEALTH-1`

---

## 5. The external wiki (issue tracker)

The wiki extends beyond this folder into the issue tracker — issues/epics/PRs are cited as heavily as doc
sections because they carry the same reconstructable context (see `README.md`). Project
**[`adammarquette/agent-forge-copilot`](https://github.com/adammarquette/agent-forge-copilot)** on GitHub.

> **Numbering caveat.** The project was migrated off its original GitLab tracker, and issue numbers did **not**
> survive the move — a `#N` minted on GitLab addresses a *different* issue on GitHub (e.g. old #109 was the
> click-to-source `Binary.read` scope; GitHub #109 is an unrelated QA-login issue). Historical `gitlab#N`
> citations throughout the docs and code comments therefore refer to the **retired** tracker and must not be
> read as GitHub links. The two tracking issues below are pre-migration numbers.

- **MVP v1** — parent tracking issue **#6** *(legacy GitLab number)*; Epics 1–12 merged (`main`).
- **Week 2 — Multimodal Evidence Agent** — saga/tracking issue **#71** *(legacy GitLab number)*; see
  `W2_ARCHITECTURE.md`.
- **The coupled OpenEMR fork** — [`agent-forge`](https://github.com/adammarquette/agent-forge); read its PHP
  source when diagnosing fork-specific auth/FHIR quirks (`README.md` §Related repositories).

> **Maintenance:** this is a navigational catalog, not storage. Update it on ingest — when a doc, `src/`
> project, requirement family, or use case is added or renamed, add/fix the row here so the jump stays live.
