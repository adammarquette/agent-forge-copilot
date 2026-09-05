# W2_PRD — AgentForge Clinical Copilot (Week 2: Multimodal Evidence Agent)

**Product:** AgentForge Clinical Copilot for Cardiology — Week 2 adds *multimodal document ingestion*, a
*small inspectable multi-agent graph*, *hybrid RAG evidence grounding*, and an *eval-driven CI gate*.
**Codebase:** .NET 10 sidecar (`agent-forge-copilot`) over the OpenEMR v8 fork (`agent-forge`).
**Target user:** Outpatient cardiologist (unchanged from Week 1).
**Author:** Adam Marquette
**Status:** Draft v0.1 — Architecture Defense deliverable (feeds `W2_ARCHITECTURE.md`).
**Last updated:** 2026-07-14
**Traces to:** Week 2 Project Requirements (Multimodal Evidence Agent) · GitLab saga **#71**.

> **How to read this document.** This is the Week 2 companion to `PRD.md`, not a replacement. Week 1
> requirements (FR-CHAT, FR-DATA, FR-VERIF, FR-AUTH, FR-OBS, FR-EVAL, and the NFRs) still hold; this doc
> specifies only what Week 2 *adds*, in the same shape: every requirement carries an ID (FR-/NFR-), a
> MoSCoW priority, acceptance criteria (AC), and the Week 1 use case(s) it serves. It is the umbrella that
> `W2_ARCHITECTURE.md` answers — this doc says *what and why*; the architecture doc says *how* (decisions
> W2-D1..D17). **The user has not changed:** it is still the outpatient cardiologist in the ~90-second
> window; Week 2 just means the "what changed" they need is now partly locked inside a scanned PDF the
> front desk uploaded. Nothing here is a capability for its own sake — if a requirement can't be traced to
> a `USERS.md` use case, it doesn't belong (Week 2's watch-word: *narrower is stronger*).

---

## 1. Document Control

| Field | Value |
|---|---|
| Version | 0.1 (draft) |
| Related deliverables | `W2_ARCHITECTURE.md`, lab_pdf/intake_form schemas, 50-case eval dataset, CI eval gate, cost/latency report, demo video |
| Deployment target | Docker container stack (`docker-compose.yml`); HIPAA-eligible cloud is the documented production target (`ARCHITECTURE.md` D15) |
| Data policy | **Demo/synthetic data only.** No real PHI at any stage — including document images, extracted fields, traces, and screenshots. |
| LLM/VLM data policy | Assume a signed no-training BAA with the model provider(s); PHI-minimization applies regardless. |
| Week 1 decision kept | **No write-back (D13)** — Week 2 adds document *ingestion* (read + derive facts) with the sidecar never writing to OpenEMR; the front desk uploads source documents natively via OpenEMR (§6 / FR-DOC-3). |
| Open decisions | Tracked in §14; deeper `[CONFIRM]` items live in `W2_ARCHITECTURE.md` §16. |

---

## 2. Executive Summary

Week 2 turns the Copilot from an agent that reads *structured* OpenEMR data into one that also **sees
clinical documents**. The physician is prepping for a follow-up; the chart has structured data, but the
information that changes today's plan is buried in a **scanned lab PDF** and a **front-desk intake form**.
The Copilot must ingest both, extract structured facts **without inventing any**, retrieve **guideline
evidence** to contextualize those facts, and return a grounded answer where every clinical claim points
back to a source — and it must stay useful when the scan is imperfect, the record is incomplete, or the
user asks a follow-up.

The engineering thesis is unchanged from Week 1: the gap between a demo and a tool a cardiologist trusts is
**trust under failure**. Week 2 raises the stakes because a vision model can hallucinate a *field label* as
confidently as a text model can hallucinate a *dose*. So the same disciplines carry forward and tighten:
**the schema is the source of truth** (raw VLM output never bypasses validation), **every clinical claim
carries machine-readable citation metadata**, **patient-record facts are never conflated with guideline
evidence**, and — the graded hard gate — **quality is proven by a 50-case eval suite behind a PR-blocking
CI gate that can actually block a merge.**

Scope is deliberately narrow: **two** document types, **two** workers plus a supervisor, **one** reranked
guideline corpus, **one** regression gate. Everything beyond that (a third document type, ColQwen2/multi-
vector indexing, a lab-trend widget, advanced contextual retrieval) is documented as stretch and deferred.
The submission is stronger for being narrower.

---

## 3. Problem Statement (the Week 2 delta)

### 3.1 The new problem
Week 1 solved *reconstruction over structured data*. But clinicians don't receive clean structured data —
they receive **the messy inputs the front desk uploads**: a lab result faxed and scanned as a PDF, an
intake form the patient filled out by hand. The most decision-relevant recent information — the out-of-range
value, the medication the patient actually reports taking, the new allergy — routinely arrives as a
**document, not a field**. An agent that can only read structured tables is blind to exactly the data that
changes today's plan.

### 3.2 Why this is hard (and why it's not just OCR)
- **Vision extraction can invent.** A VLM will read a scanned form, but it can also hallucinate a field
  label or overstate confidence on a smudged value. Unschematized, unsourced VLM output is a patient-safety
  risk, not a UX blemish. The schema, source links, and verification strategy must make unsupported
  extracted facts *visible and rejectable*.
- **Answers must separate record facts from evidence.** "Her potassium is 5.6" (a patient-record fact) and
  "ACE inhibitors are held above 5.5" (guideline evidence) are different kinds of claim with different
  sources. Conflating them is how an agent launders an opinion into a fact.
- **The architecture must stay comprehensible.** Adding multimodal input + multiple workers invites a
  black-box supervisor. Routing decisions and handoffs must be inspectable, or trust is lost at the seams.
- **A demo that can't block a regression hasn't met the bar.** The value is an *automated* quality gate,
  not a one-time working run.

### 3.3 Why this still traces to the same user
The scenario is the same cardiologist in the same ~90-second window (`USERS.md`). The document types are
chosen for that user: a **lab PDF** (the out-of-range value that changes today's plan) and an **intake
form** (what the patient reports before the physician walks in). Week 2 doesn't add a new user — it lets the
Week 1 user trust the agent even when the truth arrived as a scan.

---

## 4. Goals and Non-Goals (Week 2)

### 4.1 Goals
- **G-W2-1.** Ingest a lab PDF and an intake form, extract strict-schema structured facts, and link every
  derived fact back to its source document (page + region).
- **G-W2-2.** Never let raw vision-model output reach the user unschematized or unsourced — the schema is
  the gate.
- **G-W2-3.** Ground answers in a small clinical-guideline corpus via hybrid retrieval + rerank, keeping
  guideline evidence explicitly separate from patient-record facts.
- **G-W2-4.** Route work across a small, typed, **inspectable** supervisor + two-worker graph with logged
  handoffs.
- **G-W2-5.** Prove quality with a 50-case boolean-rubric eval suite behind a **PR-blocking** CI gate that
  fails on meaningful regression.
- **G-W2-6.** Keep source documents (OpenEMR-held) and derived facts (sidecar-owned) under **one authority per data type** — no duplicate or untraceable records, and no write-back.

### 4.2 Non-Goals (Week 2)
- **NG-W2-1.** A general medical-document AI platform. Two document types that work reliably beats five that
  don't (the case study's first-named pitfall).
- **NG-W2-2.** Writing derived Observations/DiagnosticReports back into OpenEMR — no supported FHIR/REST
  path exists in the fork; derived facts stay sidecar-owned (see §6, W2-D3).
- **NG-W2-3.** ColQwen2 / multi-vector indexing, a third document type, a lab-trend chart widget, and
  advanced contextual retrieval (query rewriting, domain filters) — all documented as stretch, deferred.
- **NG-W2-4.** Diagnosis, treatment recommendations, or autonomous orders (Week 1 NG1 still holds — the
  Copilot surfaces and cites; the clinician decides).
- **NG-W2-5.** Real PHI. Demo/synthetic data only, end to end, including document images and screenshots.

---

## 5. Target User and Use Cases

The user is the Week 1 outpatient cardiologist (`USERS.md`, unchanged). Week 2 adds three use cases that
**extend** the Week 1 set (UC-1 pre-visit brief, UC-2 grounded follow-up, UC-3 med reconciliation, UC-5
graceful degradation); each new UC anchors to a Week 1 UC and inherits its trust bar.

- **UC-6 — Document-derived pre-visit brief (extends UC-1, UC-3).**
  *Trigger:* the front desk has uploaded a scanned lab PDF and an intake form ahead of the follow-up visit.
  *Need:* the "what changed / what matters today" brief now incorporates facts extracted from those
  documents — the new out-of-range lab, the medication or allergy the patient reported — each cited back to
  the exact page and region of the source document. *Why an agent:* prioritization + synthesis now spans
  structured data *and* documents, and the extracted facts must be trusted at a glance.

- **UC-7 — Evidence-grounded answer (extends UC-2, UC-3).**
  *Need:* "What should I pay attention to, and what evidence supports the recommendation?" The agent
  retrieves relevant guideline evidence and presents it **as evidence** — visibly separate from the
  patient's record facts — so the physician can see both the value and the practice standard it's measured
  against. *Why an agent:* grounding a claim requires retrieving, reranking, and attributing evidence, then
  keeping it distinct from record facts.

- **UC-8 — Robustness to messy multimodal input (extends UC-5).**
  *Need:* the answer must remain useful when the scan is imperfect, a required field is missing or
  low-confidence, or the corpus returns no relevant evidence — stating the gap plainly rather than inventing
  a value or an evidence snippet. *Why an agent:* imperfect documents are the *normal* input, not an edge
  case; this is a first-class behavior and a rich source of eval cases.

> **Traceability rule (Week 1, still in force):** if a Week 2 capability in §7 does not point to a UC above
> (or a Week 1 UC), it is cut.

---

## 6. Product Scope (Week 2)

Week 2 expands the read-only conversational agent with a **document ingestion path**, a **hybrid evidence
retriever**, and a **small supervisor/worker graph**, surfaced in the same deployed app. In scope: document ingestion + strict-schema extraction (lab PDF, intake form); front-desk-uploaded source documents held by OpenEMR with derived facts owned by the sidecar; hybrid RAG + rerank over a small guideline corpus; a supervisor with an
intake-extractor and an evidence-retriever worker; the citation contract + click-to-source overlay; the
eval CI gate; and the Week 2 observability/cost extensions.

**Week 1's no-write-back stance (D13) is kept intact — not reopened.** The sidecar never writes clinical data to OpenEMR: the front desk uploads source documents through OpenEMR's own Documents workflow (OpenEMR is authoritative for the file), and an `oe-module-agentforge` background service forwards each new document to the sidecar's `POST /documents/ingest`, which extracts and persists **sidecar-owned derived facts** that cite the OpenEMR document. One authority per data type; nothing written twice, idempotent on a content hash (FR-DOC-3; `W2_ARCHITECTURE.md` §4, W2-D3). Out of scope: writing anything — source documents or derived Observations — back into OpenEMR, real PHI, and everything in NG-W2-3.

---

## 7. Functional Requirements

MoSCoW priority. Each FR lists acceptance criteria (AC), the use case(s) it serves, and the Week 2
requirement it satisfies. The *how* is deferred to `W2_ARCHITECTURE.md` (section refs inline).

### 7.1 Document ingestion & extraction *(Core Req 1–2 · Stage 1)*

- **FR-DOC-1 (Must) — Ingestion endpoint.** Implement `POST /documents/ingest` (called by the `oe-module-agentforge` background service) accepting a document's content, its OpenEMR `DocumentReference` id, the patient id, and `doc_type ∈ {lab_pdf, intake_form}` (closed enum). It returns strict-schema JSON and links every derived fact to the source document; it does **not** store the source (OpenEMR holds it). *Serves:* UC-6. *AC:* ingesting a lab PDF returns schema-valid JSON with a resolvable source citation per fact; an unsupported `doc_type` is rejected at the boundary. *(→ arch §3)*
- **FR-DOC-2 (Must) — Schema is the source of truth.** Raw VLM output never bypasses validation; output is
  validated against a strict schema (Pydantic/Zod/`System.Text.Json` source-gen equivalent) and anything
  that fails is rejected with a structured error and **no derived facts persisted**. *Serves:* UC-6, UC-8.
  *AC:* an injected unschematized/extra field causes rejection and zero persisted facts, logged. *(→ arch
  §3, W2-D6; NFR-CONTRACT-W2-1)*
- **FR-DOC-3 (Must) — Data authority, no write-back.** The source document is uploaded natively through OpenEMR's Documents workflow (OpenEMR is authoritative for it); the sidecar writes nothing back and owns only the derived facts, which cite the OpenEMR document. One authority per data type; re-ingesting the same content is an idempotent no-op. *Serves:* UC-6; "FHIR/OpenEMR integrity." *AC:* re-forwarding identical content returns the existing derived-fact record (content-hash idempotency); nothing is written into OpenEMR. *(→ arch §4, W2-D3)*
- **FR-DOC-4 (Must) — Required extraction fields.** `lab_pdf` extracts at least `{test_name, value, unit,
  reference_range, collection_date, abnormal_flag, source_citation}` per test; `intake_form` extracts at
  least `{demographics, chief_concern, current_medications, allergies, family_history, source_citation}`.
  *Serves:* UC-6, UC-3. *AC:* validation tests assert presence/typing of every required field, including the
  per-field source citation. *(→ arch §3)*
- **FR-DOC-5 (Should) — Confidence & derived labeling.** Every extracted fact is labeled *derived* and
  carries a field-level confidence signal; low-confidence/missing values are emitted as `null` + a flag
  rather than guessed. *Serves:* UC-8. *AC:* a smudged/absent value surfaces as low-confidence/missing, not
  a fabricated number. *(→ arch §3; feeds `factually_consistent` rubric)*

### 7.2 Hybrid RAG evidence grounding *(Core Req 3 · Stage 2)*

- **FR-RAG-1 (Must) — Small guideline corpus.** Index a small clinical-guideline corpus relevant to the
  cardiology user (practice standards the office follows). *Serves:* UC-7. *AC:* the corpus is committed to
  the repo (documents are not provided by the case study) and re-buildable from source. *(→ arch §5)*
- **FR-RAG-2 (Must) — Hybrid retrieval + rerank.** Retrieve with sparse (keyword) **plus** dense (vector)
  search, rerank candidate chunks (Cohere Rerank or equivalent), and feed only the top grounded evidence to
  the answer model. *Serves:* UC-7. *AC:* a seeded query returns reranked snippets with source metadata
  `{doc, section, chunk_id}`; retrieval hit rate is observable. *(→ arch §5, W2-D7)*
- **FR-RAG-3 (Must) — Evidence is separated from record facts.** Guideline snippets are labeled
  `source_type: guideline` and are never conflated with patient-record facts (`fhir`/`derived`); the answer
  attributes each to its own source type. *Serves:* UC-7; "evidence grounding." *AC:* an answer citing both
  a lab value and a guideline shows two distinct source types. *(→ arch §5, §7)*

### 7.3 Supervisor + two workers *(Core Req 4 · Stage 3)*

- **FR-GRAPH-1 (Must) — Inspectable supervisor.** A supervisor routes work — decide when extraction is
  needed, when evidence retrieval is needed, and when the answer is ready — over typed state, with every
  routing decision logged as an explicit handoff event carrying the correlation ID. *Serves:* UC-6, UC-7.
  *AC:* a full run's routing trail (chosen route + reason per hop) is reconstructable from logs; the
  supervisor never fabricates content, only routes. *(→ arch §6, W2-D2)*
- **FR-GRAPH-2 (Must) — Two required workers.** An **intake-extractor** worker (wraps FR-DOC-*) and an
  **evidence-retriever** worker (wraps FR-RAG-*), each with a typed handoff contract. *Serves:* UC-6, UC-7.
  *AC:* supervisor↔worker payloads conform to exported schemas (contract-tested in CI). *(→ arch §6, §9)*
- **FR-GRAPH-3 (Must) — Critic gate on the answer.** A critic node rejects claims with no resolvable
  citation and flags unsafe suggestions before release; on reject the supervisor reroutes or drops the
  claim. This reuses the Week 1 two-layer Verification (source attribution + cardiology domain constraints)
  promoted to a graph node. *Serves:* UC-3, UC-8; reuses FR-VERIF-*. *AC:* an uncited claim is suppressed,
  not released, and the rejection is logged. *(→ arch §6, W2-D8)*

### 7.4 Citation contract & click-to-source *(Core Req 5)*

- **FR-CITE-1 (Must) — Machine-readable citation on every claim.** Every clinical claim in the final
  response carries citation metadata of at least `{source_type, source_id, page_or_section,
  field_or_chunk_id, quote_or_value}`. *Serves:* UC-6, UC-7. *AC:* an eval invariant — no clinical claim
  ships without a resolvable citation object (extends Week 1 FR-VERIF-1). *(→ arch §7)*
- **FR-CITE-2 (Must) — PDF bounding-box overlay.** Document-sourced facts carry a normalized bounding box;
  the UI renders a document preview with a click-to-source overlay that highlights the cited region on the
  page. *Serves:* UC-6. *AC:* clicking a cited extracted lab value highlights its region on the source PDF
  page; low-confidence scanned regions degrade to page-level citation. *(→ arch §7, saga #71 E5)*

### 7.5 Eval-driven CI gate *(Core Req 6 · Stage 4 · HARD GATE)*

- **FR-EVAL-W2-1 (Must) — 50-case golden set.** A 50-case synthetic/demo set exercising extraction,
  evidence retrieval, citations, refusals, and missing-data behavior; no happy-path-only cases; each case
  names the failure mode it guards (extends Week 1 FR-EVAL-1). *Serves:* all. *AC:* the set is reproducible
  from the repo alone (JSON + fixture docs in `tests/`), never only in a database. *(→ arch §8, §12)*
- **FR-EVAL-W2-2 (Must) — Boolean rubrics.** Rubric categories include `schema_valid`, `citation_present`,
  `factually_consistent`, `safe_refusal`, and `no_phi_in_logs` — boolean, not 1–10. Deterministic checks
  where mechanical (`schema_valid`, `citation_present`, `no_phi_in_logs`); a fixed-prompt LLM judge only for
  `factually_consistent`/`safe_refusal`. *Serves:* all. *AC:* each case yields a boolean per applicable
  category with recorded judge config. *(→ arch §8, W2-D9)*
- **FR-EVAL-W2-3 (Must) — PR-blocking gate that blocks a real regression.** A git hook (and CI pipeline)
  runs the suite and **fails the build if any category regresses by >5% or drops below its pass threshold**.
  *Serves:* all; the graded HARD GATE. *AC:* a deliberately injected regression **fails** CI — the gate is
  built and tested first, because grading injects exactly this. *(→ arch §8)*
- **FR-EVAL-W2-4 (Must) — Adversarial & missing-data cases.** The set includes refusal cases (unsafe/uncited
  suggestions), missing/low-confidence extraction, and no-evidence-found retrieval. *Serves:* UC-8, UC-3.
  *AC:* these cases assert the correct refusal/gap-statement + logging. *(→ arch §8, §10)*

### 7.6 Observability & cost tracking *(Core Req 7)*

- **FR-OBS-W2-1 (Must) — Per-encounter Week 2 telemetry.** Each encounter logs tool sequence, latency by
  step, token usage, cost estimate, retrieval hits, extraction confidence, and eval outcome — with **no raw
  PHI**. *Serves:* all; feeds the cost/latency report. *AC:* all seven are answerable for any past Week 2
  request from telemetry. *(→ arch §10; extends Week 1 FR-OBS-2)*
- **FR-OBS-W2-2 (Must) — Week 2 dashboard.** The Week 1 dashboard is extended with document-ingestion count
  + latency, extraction field-level pass rate, RAG retrieval hit rate, reranker latency, supervisor routing
  decisions, per-worker latency, and eval pass/fail rate **per rubric category** — so a grader can tell the
  system is healthy without reading logs. *Serves:* all. *AC:* the new panels reflect live traffic. *(→ arch
  §10)*
- **FR-OBS-W2-3 (Must) — Cost & latency report.** Report actual dev spend, projected production cost,
  p50/p95 latency, and a bottleneck analysis, every number backed by measured per-query telemetry (Week 1
  §15.1 grounding rule). *Serves:* submission gate. *AC:* the report is populated from observability, not
  estimated in a vacuum. *(→ `PRD.md` §15.1)*

---

## 8. Non-Functional & Engineering Requirements (Week 2)

These are graded alongside the core submission and are **not optional** (per the case study). Each extends a
Week 1 NFR where one exists; the *how* is in `W2_ARCHITECTURE.md`.

- **NFR-CONTRACT-W2-1 (Must) — Typed contract on every new interface + canonical extraction schemas.**
  Document ingestion I/O, RAG retrieval I/O, and supervisor↔worker handoffs each have a strict, exported schema. The extraction schemas (`lab_pdf`, `intake_form`) are the canonical
  contracts — raw VLM output does not bypass them. *Extends:* NFR-CONTRACT-1. *AC:* schemas exported;
  malformed payloads rejected at the boundary with a structured error; supervisor↔worker contracts covered
  by contract tests. *(→ arch §9)*
- **NFR-MIGRATE-W2-1 (Must) — Schema evolution & migration safety.** Any schema change from Week 1 carries a
  migration note; new contracts/stores are additive; a breaking tool-schema change forces a MAJOR bump.
  Data authority is explicit — one source of truth per data type, no silent overwrites. *AC:* a migration
  note exists for any changed schema; the data-authority table (owner/lineage/access/validation) is
  documented. *(→ arch §4, §9, W2-D3)*
- **NFR-TRACE-W2-1 (Must) — Correlation ID + distributed tracing across the graph.** The Week 1 correlation
  ID propagates into document ingestion, every worker handoff, VLM/embedding/rerank calls; each worker invocation is a **child span** of the supervisor span, with extraction/retrieval
  sub-calls traceable within their worker span. *Extends:* NFR-TRACE-1 / FR-OBS-1. *AC:* a full multi-agent
  trace is reconstructable from the correlation ID alone. *(→ arch §6, §10)*
- **NFR-LOG-W2-1 (Must) — Consistent structured logging, extended not forked.** Week 2 events
  (`document_ingest_start/complete`, `extraction_outcome` per field, `retrieval_hit/miss`, `worker_handoff`,
  `eval_run_outcome`) extend the Week 1 log schema — no parallel logging convention, no plain-text output,
  no raw PHI. *AC:* Week 2 logs are searchable by case ID, event ID, and correlation ID in the Week 1 format.
  *(→ arch §10)*
- **NFR-SLO-W2-1 (Must) — SLOs, timeouts/retries, and alerts.** Define SLOs for document ingestion
  (p95 < target) and evidence retrieval; all outbound VLM/embedding/rerank/FHIR calls have timeouts +
  bounded retries with backoff; alerts fire on extraction failure rate, RAG retrieval latency, and **eval
  regression (>5% drop in any category)**, each with a documented response action. *Extends:* FR-OBS-4.
  *AC:* alert definitions exist and at least the eval-regression alert can be demonstrably triggered; the SLO
  target number is set from Week 2 baselines. *(→ arch §10)*
- **NFR-HEALTH-W2-1 (Must) — Readiness validates Week 2 dependencies.** `/ready` checks document storage
  (OpenEMR reachable), the vector index, and the reranker API, returning a **degraded** status naming the
  unavailable dependency rather than a binary up/down. *Extends:* NFR-HEALTH-1 / NFR-REL-2. *AC:* with any
  one Week 2 dependency down, `/ready` reports degraded and names it while `/health` still succeeds. *(→ arch
  §10)*
- **NFR-CI-W2-1 (Must) — CI pipeline extension.** The PR-blocking suite adds schema-validation tests,
  supervisor↔worker contract tests, and extraction regression tests to the Week 1 build/lint/typecheck/
  tests/coverage gates; dependency audit and security scan run on every PR. *AC:* a PR failing any of these
  cannot merge. *(→ arch §8, §13)*
- **NFR-API-W2-1 (Must) — OpenAPI 3.0 + runnable API collection.** Publish an OpenAPI 3.0 spec for all Week 2
  HTTP endpoints (document upload, extraction status, evidence retrieval, full Week 2 agent flow), committed
  and kept in sync via contract tests; extend the Week 1 Postman/Bruno collection so a grader can run any
  Week 2 workflow without reading source. *Extends:* FR-EVAL-4. *AC:* contract tests verify implementation
  matches the spec; the collection executes every Week 2 workflow end-to-end. *(→ arch §9)*
- **NFR-TEST-W2-1 (Must) — Documented testing strategy + fixture/stub integration tests.** Document, in
  `W2_ARCHITECTURE.md`, what is unit-tested (schema validators, tool/routing functions), integration-tested
  (ingestion→answer, RAG pipeline), evaluated via the golden set (agent behavior), and not tested and why.
  Write integration tests that exercise the full ingestion-to-answer path using **fixture documents +
  stubbed LLM/VLM responses** so they pass in CI **without live API access**. Every test names the failure
  mode it guards. *Extends:* Week 1 §8 test rule. *AC:* the strategy table exists; the fixture/stub tests are
  green in CI offline. *(→ arch §13)*
- **NFR-DATA-W2-1 (Must) — Data modeling & lineage.** Each Week 2 data type (extracted lab observation,
  intake fact, guideline chunk, citation record, source document) has a defined owner (authority), lineage,
  access control, and validation rule. *AC:* the data-model table is documented and every type has all four.
  *(→ arch §4, §9)*
- **NFR-SEC-W2-1 (Must) — PHI audit of the observability surface + CI PHI check.** Traces, logs, eval
  datasets, and cost reports must not contain patient identifiers, raw document text, or extracted clinical
  values; the scrubbing approach is documented and verified in CI by a PHI-detection check that fails the
  build on a hit (this *is* the `no_phi_in_logs` rubric). The new `DerivedFactStore` is called out as a new
  PHI-at-rest surface: encrypted, clinician-scoped, audited, never emitted to telemetry. *Extends:*
  NFR-SEC-1. *AC:* a log/trace/eval-data inspection shows no raw PHI; the CI PHI check is wired. *(→ arch
  §12)*
- **NFR-BACKUP-W2-1 (Must) — Backup & recovery.** Document how extracted documents, derived facts, and the
  eval golden set are backed up, with a manual recovery procedure and RPO/RTO estimates. The **eval golden
  set + fixtures are reproducible from the repo alone**; the guideline corpus is re-buildable from committed
  source documents. *AC:* the backup/recovery procedure and RPO/RTO are documented; the eval set does not
  live only in a database. *(→ arch §12)*
- **NFR-PERF-W2-1 (Should) — Week 2 baseline profiles.** Record baseline CPU/memory/latency/throughput for
  document ingestion, extraction, RAG retrieval, and the full multi-agent run; compare against Week 1
  baselines to confirm shared paths have not regressed. *Extends:* NFR-PERF-4. *AC:* a Week 2 baseline table
  is included and compared to Week 1. *(→ `PERFORMANCE_BASELINES.md`)*

---

## 9. Success Metrics & Acceptance

**Product-level (Week 2):**
- **M-W2-1 — Extraction integrity:** 100% of derived facts are schema-valid and carry a resolvable source
  citation; 0 unschematized VLM outputs reach the user (FR-DOC-2, FR-CITE-1).
- **M-W2-2 — Evidence separation:** 100% of guideline claims are labeled as evidence and never conflated
  with record facts (FR-RAG-3).
- **M-W2-3 — Routing inspectability:** 100% of supervisor handoffs are logged and reconstructable from the
  correlation ID (FR-GRAPH-1, NFR-TRACE-W2-1).
- **M-W2-4 — Gate efficacy (the hard gate):** an injected regression **fails** CI (FR-EVAL-W2-3).
- **M-W2-5 — Robustness:** 100% of missing-data / no-evidence / low-confidence eval cases yield a transparent
  gap statement or refusal, 0 fabrications (UC-8, FR-EVAL-W2-4).
- **M-W2-6 — PHI hygiene:** 0 raw PHI in logs, traces, eval data, or cost reports (NFR-SEC-W2-1).

> **HARD GATE (from the case study):** during grading a small regression is introduced and the CI gate must
> fail. If the eval gate does not block the regression, the Week 2 build does not pass. Corollary: build and
> test the gate itself **first**.

**Core Deliverables checklist (case study):**
- ✅ Two document types — lab PDF + intake form (FR-DOC-*).
- ✅ Supervisor + two workers — intake-extractor + evidence-retriever (FR-GRAPH-*).
- ✅ Basic hybrid RAG + rerank over a small guideline corpus (FR-RAG-*).
- ✅ 50-case golden dataset with boolean rubrics (FR-EVAL-W2-*).
- ✅ PR-blocking eval CI + observable deployed demo (FR-EVAL-W2-3, FR-OBS-W2-*).
- ✅ Critic that rejects uncited/unsafe claims — met by reuse of Week 1 Verification (FR-GRAPH-3).
- ✅ Click-to-source UI with document preview + bbox overlay (FR-CITE-2).
- ⛔ Stretch (documented, deferred): third document type; lab-trend chart widget; contextual-retrieval
  improvements (chunking/query rewriting/domain filters); ColQwen2/multi-vector.

**Submission gates:** GitLab repo (Week 1 fork + Week 2 changes, setup guide, deployed link, env-var docs);
`W2_ARCHITECTURE.md`; lab_pdf/intake_form schemas + validation tests; 50-case eval dataset + judge config +
results; CI evidence (git hook/pipeline that blocks regressions); 3–5 min demo video; cost & latency report;
publicly deployed app running the Week 2 core flow.

---

## 10. Risks & Mitigations (Week 2)

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RW1 | VLM hallucinates a field label / overstates confidence | Fabricated clinical fact | Schema is the gate; unschematized output discarded; confidence captured; `factually_consistent`+`citation_present` rubrics (FR-DOC-2/5, FR-EVAL-W2-2) |
| RW2 | Ingestion trigger lives in the fork (module background service) | Compliance/trust regression | Sidecar writes nothing to OpenEMR; ingest reachable only over the private network (trusted origin, no token); content-hash idempotency; derived facts stay sidecar-side (FR-DOC-3, arch §4, W2-D17) |
| RW3 | Derived-fact store is a new PHI-at-rest surface | HIPAA exposure | Encrypted, clinician-scoped, audited, never in telemetry; called out explicitly (NFR-SEC-W2-1) |
| RW4 | Rerank/embeddings add latency, cost, and a new dependency | Slow/expensive answers | Small corpus; provider seams; sparse-only degradation; cost tracked per query (FR-RAG-2, NFR-SLO-W2-1) |
| RW5 | Supervisor becomes a black box | Unexplainable routing | Typed state machine; every handoff logged; worker spans children of the supervisor span (FR-GRAPH-1, NFR-TRACE-W2-1) |
| RW6 | Eval gate that doesn't actually block | Fails the HARD GATE | Build/test the gate first; deterministic checks where possible; demo-injected regression must fail CI (FR-EVAL-W2-3) |
| RW7 | Prompt injection via document content | Data exfiltration/unsafe output | Document text treated as data, not instructions; authorization stays below the model (Week 1 NFR-SEC-2, FR-AUTH-3) |
| RW8 | Scanned-PDF bbox inaccuracy | Broken click-to-source | Digital path uses exact bboxes; scanned path degrades to page-level citation on low region confidence (FR-CITE-2) |
| RW9 | Scope creep (5 doc types before 2 work) | Nothing works reliably | Two doc types, two workers, one gate; everything else documented as stretch (NG-W2-3, §9) |
| RW10 | No guideline corpus provided | RAG untestable | Source + commit a small cardiology corpus, re-buildable from repo (FR-RAG-1, NFR-BACKUP-W2-1) |

---

## 11. Phasing (mapped to the four sprint checkpoints — Central/Austin)

- **Architecture Defense (4h):** this `W2_PRD.md` + `W2_ARCHITECTURE.md`; defend the all-.NET single-service
  choice, the schema-as-source-of-truth stance, the no-write-back data-authority split, and why the eval gate
  is built first.
- **MVP (Tue 11:59PM):** two document types ingesting to strict schema with citations; supervisor + two
  workers with logged handoffs; the 50-case set + PR-blocking gate **provably failing on an injected
  regression**; deployed and reachable.
- **Early Submission (Thu 11:59PM):** hybrid RAG + rerank grounding answers; click-to-source bbox overlay;
  Week 2 observability panels + alerts wired; demo video.
- **Final (Sun Noon):** full Week 2 flow deployed; cost & latency report from measured telemetry; Week 2
  baseline profiles vs Week 1; PHI-audit CI check green; README cleanly separating Week 1 baseline from
  Week 2 multimodal behavior.

---

## 12. Requirements Traceability Matrix

| Case-study problem / requirement | Week 2 requirements | Use cases | Gate deliverable |
|---|---|---|---|
| Vision extraction without invention | FR-DOC-1..5, NFR-CONTRACT-W2-1 | UC-6, UC-8 | schemas + validation tests; W2_ARCHITECTURE §3 |
| Evidence grounding | FR-RAG-1..3, FR-CITE-1 | UC-7 | W2_ARCHITECTURE §5, §7 |
| Multi-agent architecture (inspectable) | FR-GRAPH-1..3, NFR-TRACE-W2-1 | UC-6, UC-7 | W2_ARCHITECTURE §6 |
| Citation contract + click-to-source | FR-CITE-1..2 | UC-6, UC-7 | deployed UI; W2_ARCHITECTURE §7 |
| Eval-driven CI (the HARD GATE) | FR-EVAL-W2-1..4, NFR-CI-W2-1 | all | eval dataset + CI evidence |
| FHIR/OpenEMR integrity + data authority | FR-DOC-3, NFR-MIGRATE-W2-1, NFR-DATA-W2-1 | UC-6 | W2_ARCHITECTURE §4, §9 |
| Observability & cost | FR-OBS-W2-1..3, NFR-SLO-W2-1, NFR-LOG-W2-1 | all | dashboard + alerts; cost/latency report |
| Health/readiness | NFR-HEALTH-W2-1 | all | deployed `/ready` |
| API contracts (OpenAPI/collection) | NFR-API-W2-1 | all | committed spec + API collection |
| Testing strategy | NFR-TEST-W2-1 | all | W2_ARCHITECTURE §13; CI |
| HIPAA / PHI hygiene | NFR-SEC-W2-1, NFR-BACKUP-W2-1 | all | CI PHI check; backup/recovery doc |
| Failure modes / robustness | FR-DOC-5, FR-GRAPH-3, FR-EVAL-W2-4 | UC-8 | W2_ARCHITECTURE §10; eval dataset |
| Performance baselines | NFR-PERF-W2-1 | UC-6, UC-7 | PERFORMANCE_BASELINES.md |

---

## 13. Common Pitfalls (guarded, from the case study)

| Pitfall | Guarded by |
|---|---|
| Supporting five document types before two work reliably | NG-W2-3, RW9, §9 (two types only) |
| Using a VLM answer directly without schema/source metadata | FR-DOC-2, FR-CITE-1 (schema is the gate) |
| Supervisor as a black box | FR-GRAPH-1, NFR-TRACE-W2-1 (logged handoffs, child spans) |
| llm-as-a-judge without a clear rubric | FR-EVAL-W2-2 (boolean rubrics; deterministic where mechanical) |
| Logging raw document text/identifiers/screenshots to SaaS observability | NFR-SEC-W2-1 (PHI audit + CI check) |

---

## 14. Open Decisions (Week 2)

Resolved decisions and their rationale live in `W2_ARCHITECTURE.md` §15 (W2-D1..D17). Items still to
confirm before/at MVP (mirroring `W2_ARCHITECTURE.md` §16):

1. **Ingestion trigger (fork side)** — the `oe-module-agentforge` background service: the category→`doc_type` mapping and the `Document` read API used by the scan, exercised against a running OpenEMR (fork-side, agent-forge#44). 2. **Ingest trust model** — `/documents/ingest` authenticates by trusted private-network origin (no token); the shared-secret-header hardening (W2-D17) is a tracked follow-up (gitlab#91), out of scope for MVP.
3. **Vector store + providers** — pgvector vs Qdrant once corpus size is known; embeddings + reranker under
   assumed BAA.
4. **Document-ingestion p95 SLO** and derived-fact-store **RPO/RTO** — set from Week 2 baselines.
5. **Guideline corpus source** — the small cardiology corpus (not provided by the case study).
6. **Managed data-tier / cache / queue provider choices** and multi-AZ replica counts — pinned at deployment
   target selection.

---

*Draft v0.1 — Week 2 Architecture Defense deliverable. Umbrella for `W2_ARCHITECTURE.md` (the how). Companion
to `PRD.md` (Week 1). Every Week 2 requirement carries an FR-/NFR- ID, an AC, and a trace to a `USERS.md`
use case and a Week 2 case-study requirement — narrower than the original spec, and stronger for it.*
