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

**`~tok` is what the read costs** — bytes ÷ 4, rounded to 0.1K — so you can see the price before paying it.
Re-derive any of them with `scripts/measure-tok.sh`; `scripts/check-doc-sizes.sh` fails CI when a number here
stops matching its file, because a price nothing measures drifts until it inverts the advice beside it. A
blank cell means the row routes somewhere that is not one file.

| Doc | Owns / is authoritative for | ~tok | ID namespace it defines |
|---|---|---|---|
| [`PRD.md`](PRD.md) | Problem statement, functional & non-functional requirements | 11.4K | `FR-*`, `NFR-*` |
| [`W2_PRD.md`](W2_PRD.md) | **(Week 2)** Companion to `PRD.md` — what Week 2 adds (ingestion, agent graph, citations, eval gate), same FR-/NFR- shape | 9.1K | `FR-DOC-*`, `FR-GRAPH-*`, `FR-CITE-*`, `FR-EVAL-*` |
| [`USERS.md`](USERS.md) | The one persona, the 90-second workflow, use cases + capability→UC map (§5) | 3.4K | `UC-1..UC-6` |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Topology, trust boundaries, verification design, deployment; the vMVP decision log | 11.7K | doc §-numbers |
| [`W2_ARCHITECTURE.md`](W2_ARCHITECTURE.md) | **(Week 2)** Multimodal Evidence Agent — ingestion, supervisor/worker graph, hybrid RAG, eval gate | 12.6K | `W2-D1..W2-D17` |
| [`INTERFACE_CONTROL.md`](INTERFACE_CONTROL.md) | The OpenEMR external interface (FHIR R4 / OAuth2 / SMART) — the ICD | 5.7K | ICD sections A, B, … |
| [`ENGINEERING_STANDARDS.md`](ENGINEERING_STANDARDS.md) | Stack, dependencies, testing tiers, logging/security standards, solution layout (§9) | 6.2K | `§`-numbers |
| [`AUDIT.md`](AUDIT.md) | Findings from auditing the OpenEMR fork (security / perf / data quality) | 5.1K | audit finding refs |
| [`W2_AUDIT.md`](W2_AUDIT.md) | Week 2 implementation audit — per-requirement Met/Partial/Gap coverage, risks, prioritized recommendations vs the submission gates | 7.2K | audit finding refs |
| [`PERFORMANCE_BASELINES.md`](PERFORMANCE_BASELINES.md) | Measured latency/throughput baselines behind the `NFR-PERF-*` budgets (Epic 12) | 3.8K | — |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | The operational runbook for the Docker container stack — bootstrap, config, quirks, rollback; the Railway Infrastructure-as-Code deployment is §9 | 10.4K | — |
| [`CI-SETUP.md`](CI-SETUP.md) | The GitHub build/test/eval gates and the `review-verdict` gate — including why no CI job reviews code (§7) | 3.6K | — |
| [`DEPLOYMENT_TOPOLOGY.md`](DEPLOYMENT_TOPOLOGY.md) | Physical/network view of the container stack — services, what is published vs network-internal, flows, trust boundaries (mermaid) | 3.9K | — |
| [`MR_WORKFLOW.md`](MR_WORKFLOW.md) | The lifecycle a change moves through from branch to `main`, and who acts at each state — what the Coordinator drives | 0.8K | — |
| [`agents/README.md`](agents/README.md) | **Agent role contracts** — Code Reviewer, Platform, Coordinator — plus the task-sizing rubric, and for each contract whether it auto-loads or you must open it | 1.9K | — |
| [`supporting/`](supporting/) | Source requirement PDFs (Week 1 & 2) and the architecture-defense decks |  | — |

> **Agent Context Optimization:** To prevent prompt/context bloat, do **not** load full architectural specs (`ARCHITECTURE.md`, `W2_ARCHITECTURE.md`, `PRD.md`) into context upfront. Use this `INDEX.md` table to identify the single project or document section required for your immediate task.
>
> **Reaching one section without opening the doc.** `grep -n '^## ' <doc>` returns every heading with its line number for ~200 tokens; read only that range. For `ARCHITECTURE.md` that is ~730 tokens against ~11K for the whole file. The `~tok` column above prices the choice; this is how you act on it.
>
> **Sections are citable**, so cite them instead of quoting: most docs number their `##` headings (`ENGINEERING_STANDARDS.md §6`), `INTERFACE_CONTROL.md` uses `Interface A`–`D`, and `PERFORMANCE_BASELINES.md` carries the `NFR-PERF-*` ID in each heading.

## 2. Code map (the missing edge: concept → project → spec)

Layout is authoritative in [`ENGINEERING_STANDARDS.md` §9](ENGINEERING_STANDARDS.md). Each `src/` project also
carries the finer detail; this is the family-level jump table.

| Project | What it is | Implements | Specified by |
|---|---|---|---|
| `src/…Api` | ASP.NET Core host: BFF (server-side token custody), SignalR hub, `/health` + `/ready` | `FR-AUTH-*`, `FR-CHAT-*`, `NFR-HEALTH-1`, `NFR-SEC-*` | `ARCHITECTURE.md` (BFF/trust), `PRD.md` |
| `src/…Agent` | Orchestrator: multi-turn loop, tool chaining, prioritized synthesis | `FR-CHAT-*`, `FR-VERIF-0` (routing) | `ARCHITECTURE.md`, `USERS.md` (UC-1/2/6) |
| `src/…Mcp` | MCP tool server: strict tool contracts, audit log, read-only FHIR tools (min-necessary) | `FR-DATA-*`, `NFR-CONTRACT-1` | `INTERFACE_CONTROL.md`, `PRD.md` |
| `src/…Verification` | Source attribution + cardiology domain-constraint rules (INR/QT/renal/chronotropy) | `FR-VERIF-*` | `ARCHITECTURE.md` (verification), `USERS.md` (UC-3) |
| `src/…Integration.OpenEmr` | Refit clients, OAuth/SMART flow, FHIR mappers, Polly resilience | `FR-AUTH-*`, `FR-DATA-*`, `NFR-REL-*` | `INTERFACE_CONTROL.md` (ICD) |
| `src/…Llm` | `ILlmProvider` abstraction + Anthropic implementation (the provider seam) | `FR-CHAT-*` (generation) | `ARCHITECTURE.md` |
| `src/…Observability` | OpenTelemetry activity source + metrics, correlation IDs | `FR-OBS-*`, `NFR-TRACE-1`, `NFR-PERF-*` | `ENGINEERING_STANDARDS.md` §7, `PERFORMANCE_BASELINES.md` |
| `src/…Data` | **(Week 2)** EF Core + pgvector data layer: hybrid-RAG corpus + `DerivedFactStore` (entities, `DbContext`, migrations) | Week 2 persistence | `W2_ARCHITECTURE.md` §5 (W2-D14) |
| `src/…Documents` | **(Week 2)** VLM document extraction → strict schema; PdfPig word/bbox layer for citations | Week 2 ingestion (`FR-DOC-*`) | `W2_ARCHITECTURE.md` §3 |
| `src/…Retrieval` | **(Week 2)** Hybrid RAG: dense pgvector + sparse FTS + RRF fusion + Cohere rerank (`IDenseRetriever`/`IReranker`/`IEmbeddingProvider` seams) | Week 2 retrieval (`FR-RAG-*`) | `W2_ARCHITECTURE.md` §5 |
| `src/…Agents` | **(Week 2)** Evidence-agent supervisor + four workers (extract → retrieve → compose → critic) + ingestion service; logged/metered handoffs | Week 2 multi-agent graph (`FR-GRAPH-*`) | `W2_ARCHITECTURE.md` §6 |
| `tests/…UnitTests` · `…IntegrationTests` · `…EvalTests` · `…Evals` | Mocked unit tests (test-first) · real-dependency QA tests · deterministic rubric xUnit tests · golden-set eval runner (cases in top-level `evals/`) | `FR-EVAL-*` | `ENGINEERING_STANDARDS.md` §8, `tests/AGENTS.md` |
| `tools/RegisterSmartClients` | CLI utility to register confidential SMART client credentials in OpenEMR | SMART OAuth setup | `README.md` (Adding the copilot) |
| `tools/BootstrapOpenEmr` | CLI utility that writes the database half of the first-run bootstrap (OpenEMR globals + enabling the registered SMART clients), idempotently | SMART launch bootstrap | `DEPLOYMENT.md` §4 |
| `tools/SeedDemoPatients` | CLI utility to seed 20 synthetic patients, demographics only (no charts — [#416](https://github.com/adammarquette/agent-forge-copilot/issues/416)) | Demo data setup | `README.md` (Run it) |
| `tools/MintQaIdentityToken` | CLI utility to mint test identity/session tokens for QA | Integration testing | `tests/AGENTS.md` |
| `tools/LoadTestChat` | CLI load-testing tool for SignalR chat endpoints & turn latency | `NFR-PERF-*` verification | `PERFORMANCE_BASELINES.md` |
| `.github/scripts/verdict-state.sh` · `post-verdict.sh` · `watch-verdict.sh` | The verdict gate — one reader, the reviewer's poster, and the blocking watcher the authoring agent waits on | Review is a gate, not a note | `CI-SETUP.md` §8, `documentation/agents/code-reviewer.md` |
| `scripts/` · `documentation/.harness.conf` | Corpus gates: `measure-tok.sh` prints a document's price, `check-doc-sizes.sh` fails CI when a priced row stops matching its file (`.harness.conf` registers which tables are priced) | Doc/context hygiene | `CI-SETUP.md` §1 (`doc-sizes`) |
| `external/agent-forge` | The OpenEMR fork, as a submodule pinned to the commit the deployed image was built from | Deployment provenance | `DEPLOYMENT.md` §1 |
| `tools/verify-openemr-pin.sh` | Guard: the fork submodule and both OpenEMR image pins must agree (CI job `openemr-pin`) | Deployment provenance | `DEPLOYMENT.md` §1, `CI-SETUP.md` §1 |

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
sections because they carry the same reconstructable context (see `README.md`). The tracker and git remote
is **[`adammarquette/agent-forge-copilot`](https://github.com/adammarquette/agent-forge-copilot)** on
GitHub — that is where `origin` points, where issues are filed, and where pull requests are opened.

> **Legacy `gitlab#N` citations are dead links, not resolvable history.** The project lived on a GitLab
> instance (`labs.gauntletai.com`) for a period, and much of the corpus cites it. In September 2026 a
> restore recreated that project **empty** — new project id, no repository content, no issues, no tags —
> so `gitlab#N` and `!N` citations no longer resolve to anything. Keep them where they record *why* a
> decision was made; do not follow them expecting a page. A bare `#N` means the GitHub tracker above.
>
> **There is one host now.** `.github/workflows/ci.yml` fires on every push and pull request, and
> the `review-verdict` gate waits for a spawned reviewer's ruling (`CI-SETUP.md` §7–§8). Nothing needs
> mirroring, and no branch can end up without a pipeline.

- **MVP v1** — parent tracking issue **#6**; Epics 1–12 merged (`main`).
- **Week 2 — Multimodal Evidence Agent** — saga/tracking issue **#71**; see `W2_ARCHITECTURE.md`.
- **The coupled OpenEMR fork** — [`agent-forge`](https://github.com/adammarquette/agent-forge); read its PHP
  source when diagnosing fork-specific auth/FHIR quirks (`README.md` §Related repositories).

> **Maintenance:** this is a navigational catalog, not storage. Update it on ingest — when a doc, `src/`
> project, requirement family, or use case is added or renamed, add/fix the row here so the jump stays live.
