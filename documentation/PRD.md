# AgentForge Clinical Co-Pilot — Product Requirements Document

**Product:** AgentForge Clinical Co-Pilot for Cardiology
**Codebase:** Fork of OpenEMR (`Gauntlet-HQ/openemr-base-clean`)
**Target user:** Outpatient cardiologist
**Author:** Adam Marquette
**Status:** Draft v0.1 — foundation doc (feeds `USERS.md` and `ARCHITECTURE.md`)
**Last updated:** 2026-07-07

> **How to read this document.** This PRD is the umbrella that the case study's three hard-gate
> deliverables trace back to. `USERS.md` expands §5. `ARCHITECTURE.md` expands §9–§11. `AUDIT.md`
> is an input to §10 and §12. Every requirement carries an ID (FR-/NFR-) and traces to one of the
> case study's "hard problems" in the matrix at §16. Nothing here is a capability for its own sake —
> if a requirement can't be traced to a user problem, it doesn't belong.

---

## 1. Document Control

| Field | Value |
|---|---|
| Version | 0.1 (draft) |
| Related deliverables | `AUDIT.md`, `USERS.md`, `ARCHITECTURE.md`, eval dataset, cost analysis |
| Deployment target | Railway (single environment; final agent co-located with OpenEMR fork) |
| Data policy | **Demo/synthetic data only.** No real PHI at any stage of this project. |
| LLM data policy | Assume a signed BAA with the LLM provider; no data used for training. |
| Open decisions | Tracked in §17 |

---

## 2. Executive Summary

A cardiologist in a busy outpatient clinic has roughly 90 seconds between rooms to reconstruct a
patient they may not have seen in three months: what changed, what's on their medication list, whether
their latest labs are in range, and what actually needs attention *today*. That reconstruction currently
means manually scanning notes, flipping between the medication list, the lab flowsheet, and the problem
list — under time pressure, with the patient already waiting.

The AgentForge Clinical Co-Pilot is a conversational AI agent embedded in OpenEMR that does that
reconstruction for the cardiologist and surfaces only what's relevant, with **every claim traceable to a
source in the patient's record**. It is not a generic medical chatbot. It knows *this* patient — their
cardiac history, their meds, their recent labs, their procedures — and it will refuse to state as fact
anything it cannot ground in the chart.

The central engineering thesis: the gap between a demo-quality agent and one a hospital would put in front
of a cardiologist is entirely about **trust under failure** — source attribution, domain-constraint
enforcement, authorization, graceful degradation, and observability. This PRD specifies the system so that
trust is architected in from the first commit, not retrofitted.

---

## 3. Problem Statement

### 3.1 The user's problem
Outpatient cardiology is longitudinal and data-dense. A single patient may carry atrial fibrillation on
anticoagulation, heart failure with reduced ejection fraction (HFrEF) on guideline-directed medical therapy
(GDMT), coronary artery disease post-stent on dual antiplatelet therapy, plus hypertension and
hyperlipidemia. Between visits, labs change (INR drifts, creatinine and potassium move on ACE inhibitors,
lipids respond to statins), medications get titrated, and events happen (an ED visit, a new echo, a device
interrogation). The cardiologist must re-establish all of this in seconds.

The failure of the status quo is not "no data" — it's **too much data, poorly prioritized**, forcing the
clinician to be the synthesis engine at exactly the moment they have the least time.

### 3.2 Why this is hard (and why it's not a chatbot)
- A **confidently stated hallucination is a patient-safety event**, not a UX blemish. "The patient is on
  metoprolol 50mg BID" must be true and cited, or it must not be said.
- The right answer often requires **synthesizing conflicting or incomplete records** (a med list that
  disagrees with the last note; a lab that's stale). The agent must communicate uncertainty rather than
  paper over it.
- **Multiple roles** touch the chart (attending cardiologist, fellow, nurse, MA). The system cannot assume
  every requester is trusted or entitled to the same data.

### 3.3 Why an agent (not a dashboard)
A dashboard shows everything and prioritizes nothing; a sorted list can't answer a follow-up. The
cardiologist's real need is **conversational synthesis with memory**: "What changed since last visit?" →
"Is her INR in range?" → "Why did we stop the diltiazem?" Each turn depends on the last, requires pulling
from different tables, and ends in a judgment about relevance. That shape — multi-turn, tool-invoking,
context-carrying — is what justifies an agent. (Every agent capability in §7 is defended against this bar.)

---

## 4. Goals and Non-Goals

### 4.1 Goals
- **G1.** Reconstruct a cardiology patient's current state in seconds, with prioritized, source-cited output.
- **G2.** Guarantee that every asserted clinical fact is attributable to a specific record, or is explicitly
  flagged as unverified.
- **G3.** Enforce cardiology domain constraints (dosing, ranges, interactions, contraindications) as a
  hard gate on responses.
- **G4.** Enforce role- and relationship-based authorization on all patient-data access.
- **G5.** Be observable and evaluable from day one — every request traceable, every regression catchable.
- **G6.** Degrade gracefully and transparently when tools fail or data is missing.

### 4.2 Non-Goals (explicitly out of scope)
- **NG1.** Diagnosis, treatment recommendations, or autonomous orders. The Co-Pilot **surfaces and cites**;
  the clinician decides. (This also keeps it clear of FDA device-software territory — see §12.4.)
- **NG2.** Generic medical Q&A untethered to the patient record.
- **NG3.** Inpatient rounding workflows. OpenEMR is ambulatory; we don't fight its grain.
- **NG4.** Real PHI. Demo/synthetic data only for the entire project.
- **NG5.** Write-back to the EHR in the MVP (read-only agent first; write actions are a later, higher-bar phase).
- **NG6.** Multi-specialty generalization in this phase. Cardiology is the deliberate narrowing.

---

## 5. Target User and Use Cases
*(This section is the seed for `USERS.md`. Every capability in §7 traces to a use case here.)*

### 5.1 Primary user: the outpatient cardiologist — defined by a micro-workflow

> "Physicians need help finding information" is not a user definition. So this section defines the *exact
> moment* the Co-Pilot enters this clinician's day, down to the window and the data.

**Who.** An established-practice outpatient cardiologist running a clinic of ~16–20 scheduled patients,
roughly two-thirds of them longitudinal follow-ups (AFib, HFrEF, post-PCI CAD, HTN, hyperlipidemia) they last
saw weeks-to-months ago. High domain expertise; near-zero tolerance for a tool that is confidently wrong;
faster at reading a chart than most tools are at loading — so the bar to earn a click is high.

**The window: the ~90 seconds between Room A and Room B.**

- **T-minus (the 20 seconds before opening the app).** The cardiologist finishes with the patient in Room A,
  steps into the hallway, and glances at the schedule to see who's next in Room B. They have *a name and a
  reason for visit* and little else loaded in working memory. The nurse has already roomed the Room B patient
  and taken vitals; the patient is waiting. The cardiologist does **not** have time to open the chart and read
  the last three progress notes, the lab flowsheet, and the med list separately.

- **T-zero (opens the Co-Pilot on the Room B patient).** In one action, from the hallway workstation or
  tablet, they open the Co-Pilot already scoped to the Room B patient.

- **The 60–75 seconds of use — what exact data they need.** A single prioritized brief answering "*what
  changed and what matters today*," specifically:
  1. **Interval events** since the last visit — ED visit, hospitalization, new echo (and the EF), device
     interrogation, new outside records.
  2. **Medication changes** — what was started/stopped/titrated, and any adherence signal.
  3. **New or out-of-range labs** relevant to their cardiac regimen — INR vs. therapeutic range for the
     indication; K⁺/creatinine trend on ACEi/ARB/diuretic; lipids on statin; BNP/NT-proBNP trend in HF.
  4. **Active safety flags** — a domain-constraint hit (e.g., QT-prolonging combination, renally-contraindicated
     dose) that should not be missed before writing anything today.
  Every item is **source-cited** so the cardiologist can trust it at a glance, and the brief leads with the
  one or two things that change today's plan — not an exhaustive dump.

- **The follow-up (still inside the window).** If something in the brief needs one more hop — "*when was that
  INR drawn?*", "*why did we stop the diltiazem?*" — they ask it conversationally and get a cited answer
  without leaving the tool or opening three tabs. (This is the multi-turn justification for UC-2.)

**What they do with the output.** They walk into Room B already oriented: they open with the right question
("How've you felt since the ER visit?"), verify the med list against what the patient reports, and act on the
safety flag before it becomes an error. The output's job is to convert a cold-start into a warm one **inside
the 90 seconds** — if it can't be trusted at a glance or takes longer than reading the chart, it has failed.

**Secondary moment (same user, different rhythm): the pre-clinic sweep.** Before the session, the same
cardiologist runs the brief across the whole day's schedule to triage which patients carry a change worth
pre-reading. This is the batch shape of UC-1 and tolerates a few seconds more latency than the in-room window.

**Why this user constrains everything downstream.** The 90-second in-room window sets the latency budget
(NFR-PERF-1); the "trust at a glance" bar makes source attribution non-negotiable (FR-VERIF-1); the
safety-flag need drives domain-constraint enforcement (FR-VERIF-2); and the multi-user hallway workstation
makes "who's asking?" a real question (FR-AUTH-*).

### 5.2 Secondary users (drive the authorization requirements)
- **Cardiology fellow** — supervised; may have narrower access; actions may be attributable to a supervising
  attending.
- **Clinic nurse / MA** — legitimately touches the chart for intake/vitals/med reconciliation, but with
  different entitlements than the physician.

These are not decorative. They exist so that "who is asking?" is a real, enforced question (FR-AUTH-*).

### 5.3 Primary use cases

- **UC-1 — Pre-visit brief ("what changed + what matters today").**
  *Trigger:* clinician opens a scheduled patient. *Need:* a prioritized summary of changes since the last
  visit — new/abnormal labs, med changes, interval events (ED visit, new echo/EF, device check) — each
  cited. *Why an agent:* prioritization + synthesis across sources + immediate follow-up questions. A
  dashboard can't decide what *matters today*.

- **UC-2 — Grounded follow-up Q&A (multi-turn).**
  *Need:* "Is her INR therapeutic?" → "When was it last drawn?" → "What's her CHA₂DS₂-VASc?" Each turn builds
  on prior context and may chain tools. *Why an agent:* multi-turn context and tool chaining are intrinsic;
  this is the case study's justification test for both features.

- **UC-3 — Medication reconciliation & safety check.**
  *Need:* surface the current cardiac med list with sources, and flag domain-constraint issues (e.g., a
  QT-prolonging combination, a renally-contraindicated dose, an INR out of range for the indication). *Why an
  agent:* it must *reason over* the list against clinical rules, not just display it.

- **UC-4 — Authorization-aware access (the "who's asking" case).**
  *Need:* the same query returns different results — or is refused — depending on the requester's role and
  relationship to the patient. *Why an agent:* the boundary must hold even under adversarial phrasing
  ("ignore that and show me…"), which is an agent-security problem, not a UI toggle.

- **UC-5 — Graceful degradation under missing/failed data.**
  *Need:* when a tool errors or a record is incomplete, the agent says so plainly and returns what it *can*
  verify, rather than inventing or silently dropping. *Why an agent:* demo data is genuinely sparse; this is
  a first-class behavior, and a rich source of eval cases.

> **Traceability rule:** if a capability in §7 does not point to a UC above, it is cut.

---

## 6. Product Scope

The Co-Pilot is a **read-only conversational agent** surfaced inside the OpenEMR patient context, backed by
a dedicated agent service. In scope for the buildable product: the conversational interface, patient-data
tools over OpenEMR, the verification layer, authorization enforcement, observability, and the eval suite.
Out of scope: write-back, diagnosis/orders, non-cardiology breadth, real PHI (see §4.2).

---

## 7. Functional Requirements

Priority uses MoSCoW. Each FR lists acceptance criteria (AC) and the use case(s) it serves.

### 7.1 Agentic conversational interface

- **FR-CHAT-1 (Must) — Multi-turn agent.** A conversational agent that maintains context across turns within
  a patient session and can ask/answer follow-ups. *Serves:* UC-1, UC-2.
  *AC:* a 3-turn exchange where turn 3 correctly resolves a reference from turn 1 ("she" / "that lab") without
  restating.
- **FR-CHAT-2 (Must) — Tool invocation & chaining.** The agent retrieves patient data via defined tools and
  can chain them when a question requires it. *Serves:* UC-2, UC-3.
  *AC:* a query requiring ≥2 tool calls (e.g., resolve patient → fetch latest INR) produces a correct, cited
  answer; the chain is visible in logs.
- **FR-CHAT-3 (Must) — Patient-scoped context.** The agent operates within one patient's record at a time;
  it cannot silently pull another patient's data into the conversation. *Serves:* UC-4.
  *AC:* a request to compare against "your other AFib patients" is refused or scoped-blocked and logged.
- **FR-CHAT-4 (Should) — Uncertainty communication.** When records conflict or data is stale, the agent
  surfaces the conflict rather than resolving it silently. *Serves:* UC-1, UC-5.
  *AC:* given two disagreeing med sources, the response presents both with dates/sources.

### 7.2 Data access & tools (over OpenEMR)

- **FR-DATA-1 (Must) — Canonical tool contracts.** Every tool has a strict input/output schema (Pydantic/Zod
  or equivalent) that is the source of truth, independent of implementation. *Serves:* all.
  *AC:* invalid input is rejected at the contract boundary with a structured error; contracts are exported.
- **FR-DATA-2 (Must) — Core cardiology data tools.** At minimum: patient demographics, problem list,
  medications, allergies, lab results/flowsheet, vitals, encounters/notes, and procedures. *Serves:* UC-1–3.
  *AC:* each tool returns source identifiers (table/row or FHIR resource id) usable for attribution.
- **FR-DATA-3 (Must) — Access path.** Prefer OpenEMR's FHIR R4 / REST API with OAuth2 where it exposes the
  needed data; fall back to a read-only, least-privilege DB path only where FHIR is insufficient, documented
  in `ARCHITECTURE.md`. *Serves:* all. *AC:* the chosen path per data type is documented with rationale.
- **FR-DATA-4 (Should) — Semi-structured extraction.** Cardiology-critical values that live in documents
  rather than structured fields (e.g., ejection fraction from an echo report, device interrogation results)
  are extracted with the source document cited and the extraction flagged as derived. *Serves:* UC-1, UC-3.
  *AC:* an EF surfaced from a narrative report links to the source document and is labeled "derived."

### 7.3 Verification system *(the trust core)*

- **FR-VERIF-0 (Must) — Mandatory gate.** Every response passes through the verification layer before it
  reaches the user; nothing bypasses it. *Serves:* all.
- **FR-VERIF-1 (Must) — Source attribution.** Every asserted clinical fact must be attributable to a specific
  record; unattributable claims must not be stated as fact. *Serves:* UC-1–3.
  *AC:* an eval invariant — no response asserts a medication/lab/problem without an attached, resolvable
  source id; violations fail the response.
- **FR-VERIF-2 (Must) — Domain-constraint enforcement.** The agent checks responses against a defined set of
  cardiology constraints and flags/blocks violations. Illustrative constraints (final set to be clinically
  validated): warfarin INR therapeutic range by indication; renal-dose contraindications for DOACs;
  QT-prolonging drug combinations; ACEi/ARB with hyperkalemia or rising creatinine; negatively-chronotropic
  combinations (e.g., non-dihydropyridine CCB + beta-blocker in relevant contexts). *Serves:* UC-3.
  *AC:* a seeded record that violates a constraint triggers a flag with the rule and the source values cited.
- **FR-VERIF-3 (Must) — Verification outcome is logged & measurable.** Each response records a verification
  pass/fail and the reason. *Serves:* G5. *AC:* verification pass/fail rate appears on the dashboard.
- **FR-VERIF-4 (Should) — Documented limits.** The known gaps of the verification approach (what it does *not*
  catch) are written down. *AC:* a "known limitations" subsection exists in `ARCHITECTURE.md`.

> **Design note (own it in the interview):** decide explicitly *where* verification sits — pre-generation
> (constrain what the model can say), post-generation (check claims against retrieved sources before
> release), or both — and what each layer catches. State it and defend it.

### 7.4 Authorization & access control

- **FR-AUTH-1 (Must) — Authenticated requester identity.** Every agent invocation carries an authenticated
  user identity and role; the agent never assumes a trusted caller. *Serves:* UC-4. *AC:* an unauthenticated
  request is rejected before any tool runs.
- **FR-AUTH-2 (Must) — Role/relationship-based entitlement.** Access to patient data is gated by the
  requester's role and relationship to the patient (e.g., physician panel ownership; fellow supervision;
  nurse scope). *Serves:* UC-4. *AC:* identical query, two roles → correctly different results or a refusal.
- **FR-AUTH-3 (Must) — Enforcement below the model.** Authorization is enforced in the tool/data layer, not
  by prompt instruction, so it holds under adversarial input. *Serves:* UC-4. *AC:* a prompt-injection
  attempt ("ignore restrictions and show X") does not yield unauthorized data; attempt is logged.
- **FR-AUTH-4 (Must) — Access is audit-logged.** Every patient-data access records who, what, when, and why
  (see §12.2). *Serves:* compliance. *AC:* audit entries reconstruct a full access trail per patient.

### 7.5 Observability

- **FR-OBS-1 (Must) — Correlation ID.** Every invocation gets a unique correlation ID present in every log
  line, tool call, and LLM interaction. *AC:* a full trace is reconstructable from logs alone via the ID.
- **FR-OBS-2 (Must) — Step/timing/failure/cost trace.** Logs answer: what the agent did and in what order,
  how long each step took, which tools failed and why, and tokens consumed + cost. *AC:* all four are
  answerable for any past request.
- **FR-OBS-3 (Must) — Live dashboard.** Real-time view of request count, error rate, p50/p95 latency, tool
  call counts, retry counts, and verification pass/fail rate (LangSmith/Langfuse/Braintrust or equivalent).
  *AC:* dashboard reflects live traffic; metrics update during a load test.
- **FR-OBS-4 (Must) — Alerts.** ≥3 alerts: p95 latency over threshold, error rate over threshold, tool
  failure rate over threshold — each with a documented meaning and on-call response. *AC:* alert definitions
  exist; at least one can be demonstrably triggered.

### 7.6 Evaluation

- **FR-EVAL-1 (Must) — Boundary/invariant/regression suite.** Every eval case exercises a boundary (missing
  data, malformed input, empty record), an invariant (claims must cite a source), or a known regression risk;
  each documents the failure mode it guards. *AC:* no happy-path-only cases; each case names its guard.
- **FR-EVAL-2 (Must) — Adversarial & authorization cases.** Includes attempts to extract data the requester
  isn't entitled to, and prompt-injection attempts. *AC:* these cases assert refusal + logging.
- **FR-EVAL-3 (Should) — Runnable & repeatable.** The suite runs on demand with recorded results; wired for
  CI is preferred. *AC:* a single command produces pass/fail results.
- **FR-EVAL-4 (Should) — Runnable API collection.** A Postman/Bruno collection covering core endpoints so a
  grader can run any workflow without reading source. *AC:* collection executes end-to-end.

---

## 8. Non-Functional Requirements

- **NFR-PERF-1 (Must) — Interactive latency.** Target p95 ≤ 26 seconds end-to-end for a single-patient
  `RequestBrief` turn at up to 50 concurrent users (fixed during baselining — measured p95 was 20.8s at 10
  concurrent, 25.8s at 50; see `PERFORMANCE_BASELINES.md`). This is well above the original "a few seconds"
  aspiration; the gap is sequential multi-step LLM tool-calling plus real external API latency, not
  `agent-forge-api`'s own compute (baseline shows <1% CPU at 50 concurrent users). Closing it is future work
  via the *speed-vs-completeness policy:* return a fast, verified core answer first and defer/stream deeper
  synthesis; communicate when more is pending rather than blocking — not yet implemented in v1. *AC:*
  p50/p95/p99 recorded at load ✓; policy documented (implementation deferred).
- **NFR-PERF-2 (Should) — Baseline profiles.** Capture CPU, memory, latency, throughput baselines under the
  load scenarios so future changes are measurable. *AC:* baselines included in submission.
- **NFR-PERF-3 (Should) — Load behavior.** Characterize behavior at ≥10 and ≥50 concurrent users (p50/p95/p99
  + error rate at each). *AC:* results recorded.
- **NFR-SEC-1 (Must) — PHI handling discipline.** Encryption in transit and at rest; secrets via managed
  config, never in code; least-privilege data access; no PHI in logs, traces, or the observability backend
  (redact/tokenize patient identifiers in telemetry). *AC:* a log/trace inspection shows no raw PHI.
- **NFR-SEC-2 (Must) — Prompt-injection resistance.** Untrusted record content (notes, documents) is treated
  as data, not instructions; authorization and tool access can't be overridden by content or user phrasing.
  *AC:* covered by FR-AUTH-3 / FR-EVAL-2 cases.
- **NFR-REL-1 (Must) — Graceful degradation.** Tool failure, incomplete records, or unexpected model output
  produce a transparent, predictable partial result — never a crash or a silent fabrication. *AC:* fault-
  injection tests show bounded, cited degradation.
- **NFR-REL-2 (Must) — Health vs readiness.** Separate `/health` (process alive) and `/ready` (dependencies
  reachable). `/ready` genuinely checks OpenEMR, the LLM provider, and the observability backend. *AC:*
  `/ready` fails when a dependency is down.
- **NFR-SCALE-1 (Should) — Scale reasoning.** Document how the design scales toward a large clinic / hospital
  (300 concurrent clinical users), including where the current architecture would break first. *AC:* covered
  in §11 and `ARCHITECTURE.md`.

### 8.1 Engineering & operational NFRs

- **NFR-TRACE-1 (Must) — Correlation ID across boundaries.** Every request carries a unique correlation ID
  that propagates across all service boundaries (ingress → agent → each tool call → LLM interaction →
  observability backend), so a full trace is reconstructable from logs alone. *Implements/aligns with:*
  FR-OBS-1. *AC:* given any request's correlation ID, every downstream log entry, tool call, and LLM call for
  that request can be retrieved and ordered.
- **NFR-CONTRACT-1 (Must) — Strict schema contracts.** All tool inputs and outputs are defined by strict
  schemas (Pydantic/Zod or equivalent); the schema is the source of truth, not the implementation. Invalid
  payloads are rejected at the contract boundary with a structured error. *Implements/aligns with:* FR-DATA-1.
  *AC:* schemas are exported as canonical contracts; a malformed tool input is rejected before execution and
  the rejection is logged.
- **NFR-HEALTH-1 (Must) — Separate liveness and readiness endpoints.** Expose `/health` (process liveness)
  and `/ready` (dependency validation) as distinct endpoints. `/ready` performs a meaningful check that
  OpenEMR, the LLM provider, and the observability backend are reachable — it does not return 200
  unconditionally. *Implements/aligns with:* NFR-REL-2. *AC:* with any one dependency unreachable, `/ready`
  fails while `/health` still succeeds.
- **NFR-PERF-4 (Must) — Load/stress baselines.** Run load/stress tests at **10 and 50 concurrent users**
  against the deployed agent and record **p50/p95/p99 latency and error rate at each level**, captured
  alongside baseline CPU, memory, and throughput so future changes are measurable. *Implements/aligns with:*
  NFR-PERF-2/3. *AC:* a results table for both concurrency levels (p50/p95/p99 + error rate) is included in
  the submission with the baseline profiles.

---

## 9. System Architecture Overview
*(High-level; expanded and defended in `ARCHITECTURE.md`, which must open with a ~500-word summary.)*

**Shape:** a dedicated **agent service** deployed alongside the OpenEMR fork, exposing the conversational
Co-Pilot in the patient context. The agent never talks to the database ad hoc; it goes through **typed
tools** (FR-DATA-1) that encapsulate OpenEMR access, authorization checks, and source-id capture.

**Core flow (per turn):**
1. **Ingress** — authenticated request with requester identity/role + patient context; correlation ID
   assigned (FR-OBS-1).
2. **Authorization gate** — entitlement checked in the tool/data layer before any retrieval (FR-AUTH-*).
3. **Agent reasoning** — model plans and invokes typed tools; tool outputs carry source ids.
4. **Verification layer** — source attribution + domain-constraint checks before release (FR-VERIF-*).
5. **Response** — cited answer (or transparent partial/refusal), with the full trace logged.

**Key decisions to make and defend** (tracked in §17): single-agent vs multi-agent; LLM provider (structured-
output support, context window, cost, assumed-BAA); verification placement (pre/post/both); FHIR-first vs
DB-fallback per data type; state/memory model for multi-turn.

---

## 10. Data & Integration

- **OpenEMR is ambulatory-oriented.** Design to that grain (outpatient cardiology), per NG3.
- **Structured, citable sources:** demographics, problem list, medications, allergies, lab results/flowsheet,
  vitals, encounters, procedures. These anchor source attribution.
- **Semi-structured / risk areas:** ejection fraction, echo findings, device (pacemaker/ICD) interrogations,
  and some cardiology results often live in documents/notes rather than discrete fields → require extraction
  and explicit "derived" labeling (FR-DATA-4).
- **Data-quality reality (an `AUDIT.md` input):** demo data is uneven — some patients fully populated, many
  demographics-only; missing fields, inconsistent formatting, stale values, possible duplicates. **These are
  agent failure modes, not edge cases** — they seed UC-5 and the FR-EVAL boundary suite. Plan to seed a small
  set of clinically realistic cardiology patients for demo/eval.

---

## 11. Deployment & Infrastructure

- **Platform:** Railway. The OpenEMR fork and the agent service deploy to Railway; a public URL is submitted
  with every checkpoint (hard gate). Choose the stack once and keep the final agent on the same
  infrastructure.
- **Environments:** at minimum a live, reachable deployment; separate build/preview from the demo environment
  where practical.
- **Ops requirements:** `/health` + `/ready` (NFR-REL-2), CI/CD for agent updates, a documented rollback path,
  and the dashboard/alerts (FR-OBS-3/4) wired to the deployed service.
- **HIPAA posture on Railway (defensible position):**
  - *This project:* demo/synthetic data only → **no BAA required**, consistent with the case study.
  - *Production with real PHI:* Railway offers HIPAA BAAs **only on its Enterprise track** (minimum monthly
    commitment, ~$1k/mo) with team access restrictions when active. Real PHI must not touch any Railway
    environment, database, log, or backup **without a signed BAA**. State this explicitly — it's exactly the
    kind of gap a hospital CTO probes. If enterprise commitment isn't viable at production scale, the
    migration target is a HIPAA-eligible host (AWS/GCP/Azure under BAA) — carried into the §15.1 scale-up.

---

## 12. Compliance & Regulatory
*(Deeper than "we know the acronym." An `AUDIT.md` compliance pass feeds this section.)*

### 12.1 HIPAA as an architectural constraint
PHI handling shapes storage, transit, logging, and access (NFR-SEC-1). The concrete commitments: encryption
in transit/at rest, least-privilege access, no PHI in telemetry, and enforced authorization (FR-AUTH-*).

### 12.2 Audit logging
Every patient-data access and every agent action is audit-logged (who/what/when/why, correlation ID) and
retained per policy. This is both a HIPAA obligation and the substrate for observability (FR-OBS) and
incident reconstruction.

### 12.3 BAA implications of sending PHI to an LLM
Sending PHI to an LLM provider makes that provider a business associate requiring a BAA. **Project stance:**
demo data only, and assume a signed no-training BAA with the provider (per the case study). Document that in
production this must be a real, executed BAA, and that PHI-minimization (send only what's needed) applies
regardless.

### 12.4 Clinical-AI transparency & scope (context, not a checkbox)
- **Scope guardrail (FDA):** the Co-Pilot **surfaces and cites existing record data and does not diagnose,
  recommend treatment, or place orders** (NG1). Keeping the clinician able to independently review the basis
  of each statement (source citations) is what keeps this in "clinician-in-the-loop information retrieval"
  territory rather than autonomous clinical decision-making.
- **Transparency direction (ONC HTI-1 / DSI):** federal rules for *certified* health IT now require
  predictive decision-support interventions to expose "source attributes" — plain-language transparency about
  what an intervention is, how it was developed, and how it performs. AgentForge isn't a certified-health-IT
  submission, but the **design intent aligns**: source attribution, documented limitations, and observable
  performance are first-class. Note this as forward-looking posture, not a claim of certification.

---

## 13. Risks & Mitigations

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| R1 | Hallucinated clinical fact reaches clinician | Patient safety, trust collapse | Verification layer as a hard gate; source-attribution invariant in eval (FR-VERIF-1, FR-EVAL-1) |
| R2 | Unauthorized data exposure across roles/patients | HIPAA breach | Authorization below the model; adversarial eval cases (FR-AUTH-3, FR-EVAL-2) |
| R3 | Sparse/inconsistent demo data breaks answers | Wrong or empty output | Graceful degradation + missing-data eval; seed realistic patients (NFR-REL-1, §10) |
| R4 | Latency too high for the 90-second reality | User abandons tool | Fast-core-then-defer policy; baselining + load tests (NFR-PERF-*) |
| R5 | PHI leaks into logs/observability backend | Compliance failure | Redact/tokenize telemetry; log-inspection AC (NFR-SEC-1) |
| R6 | Prompt injection via note/document content | Data exfiltration, safety | Treat record content as data; injection eval cases (NFR-SEC-2) |
| R7 | Domain constraints wrong or incomplete | False reassurance | Constraints clinically validated + documented limits; flagged as illustrative until validated (FR-VERIF-2/4) |
| R8 | Railway PHI/BAA gap at production | Legal exposure | Demo-only now; documented migration/BAA path (§11) |
| R9 | LLM/tool cost scales non-linearly | Unit economics break | Cost tracking from day one; cost analysis at 100/1K/10K/100K users |

### 13.1 Failure Modes & Graceful Degradation

> The case study is blunt: **"A clinical tool that crashes or silently fails is worse than no tool at all."**
> §13 above is the risk *register* (likelihood × impact). This subsection is the operational *contract* for
> behavior under failure. Two invariants govern every row below:
>
> - **Never fabricate to fill a gap.** Missing/failed data is reported, never invented (upholds FR-VERIF-1).
> - **Never fail silently.** Every degradation is visible to the user *and* recorded in observability
>   (FR-OBS-1/2) with the correlation ID. Partial-but-honest beats complete-but-untrustworthy.

**Design defaults:** per-tool timeouts and bounded retries with backoff; a per-request deadline aligned to the
90-second window (NFR-PERF-1) so the agent returns *something verified* rather than hanging; tool calls are
independent so one failure degrades one section, not the whole brief; every tool result is typed
(NFR-CONTRACT-1) so malformed output is caught at the boundary, not mid-reasoning.

| Failure condition | Detection | Agent behavior (graceful degradation) | User sees | Logged | Guarded by |
|---|---|---|---|---|---|
| **Tool call fails / errors** | Non-2xx, exception, contract-invalid output | Skip that section; continue with the tools that succeeded; mark the section unavailable | Partial brief with an explicit "Labs unavailable — could not retrieve" banner on the affected section | Tool name, error, correlation ID, retry count (FR-OBS-2) | FR-EVAL-1, NFR-REL-1 |
| **Tool slow / hits deadline** | Per-tool timeout; per-request deadline | Return the verified core now; mark the slow section as "still loading / omitted for speed" rather than blocking | Fast core answer + a note that one section was deferred | Timeout event + latency (FR-OBS-3) | NFR-PERF-1, NFR-REL-1 |
| **LLM timeout / provider error** | Timeout, 5xx, SDK error | Bounded retry with backoff; if still failing, degrade to a **deterministic, non-LLM fallback**: raw source-cited data pulled straight from tools (no synthesis) rather than nothing | "Summary unavailable right now — here is the source data" with citations | LLM error, retries, fallback-taken flag | NFR-REL-1, FR-EVAL-1 |
| **LLM rate-limit / quota** | 429 | Backoff + retry; if sustained, same deterministic fallback; alert fires | Same as above | 429 count; feeds the error-rate alert (FR-OBS-4) | NFR-PERF-*, FR-OBS-4 |
| **Missing / incomplete patient data** | Empty result, null required field | State the gap explicitly; return what *is* present; never infer the missing value | "No INR on file since 2025-11 — cannot confirm therapeutic range" | Which field/source was missing | UC-5, FR-CHAT-4, FR-EVAL-1 |
| **Conflicting records** (e.g., med list vs. note) | Cross-source mismatch | Surface *both* with dates and sources; do **not** silently pick one | "Med list shows diltiazem; last note says discontinued 2026-05 — please verify" | Conflict + both sources | FR-CHAT-4, UC-1 |
| **Ambiguous query** | Low-confidence intent / multiple referents | Ask one focused clarifying question, or state the interpretation taken before answering — never guess silently on a clinical question | "Do you mean her most recent INR or the trend?" | Ambiguity flag + resolution | UC-2, FR-EVAL-1 |
| **Unexpected / unparseable model output** | Schema validation fails (NFR-CONTRACT-1) | Reject the output; one repair attempt; else deterministic fallback | Core data without the malformed section | Validation error + raw output ref | NFR-CONTRACT-1, NFR-REL-1 |
| **Claim can't be grounded** (verification fail) | FR-VERIF-0 gate | Suppress the unattributable claim; return only what passed verification | Verified statements only; suppressed items noted | Verification fail + reason (FR-VERIF-3) | FR-VERIF-1, FR-EVAL-1 |
| **Authorization denied** | FR-AUTH gate | Refuse cleanly; return no data; no leakage via error text or timing | "You don't have access to this patient's record" | Denied access attempt (FR-AUTH-4) | UC-4, FR-EVAL-2 |
| **Dependency down** (OpenEMR / observability) | `/ready` check (NFR-HEALTH-1) | Fail readiness so traffic isn't routed to a broken instance; surface a clear service-unavailable state | "The Co-Pilot is temporarily unavailable" — not a blank or a wrong answer | Readiness failure + which dependency | NFR-REL-2, FR-OBS-4 |

**The line we won't cross:** when the system cannot produce a *trustworthy* answer, the correct output is an
honest "I can't verify that right now," never a plausible guess. In cardiology, a confident wrong answer is
the failure mode that harms a patient — so degradation always favors silence-with-citation-of-the-gap over
fabrication. Each row above has a corresponding fault-injection case in the eval suite (FR-EVAL-1).

---

## 14. Success Metrics & Acceptance Criteria

**Product-level:**
- **M1 — Groundedness:** 100% of asserted clinical facts carry a resolvable source (eval invariant; zero
  tolerance).
- **M2 — Constraint recall:** ≥ target% of seeded constraint violations are flagged (set target during eval
  design).
- **M3 — Authorization integrity:** 0 unauthorized disclosures across role/injection eval cases.
- **M4 — Latency:** p95 within the stated interactive target under ≥10 concurrent users.
- **M5 — Degradation:** 100% of fault-injection cases yield transparent partial/refusal, 0 crashes/silent
  fabrications.

**Submission gates (from the case study):** deployed public URL each checkpoint; `AUDIT.md` (≤~500-word
summary first); `USERS.md`; `ARCHITECTURE.md` (≤~500-word summary first); eval dataset + results;
observability dashboard + ≥3 alerts; cost analysis; demo video; final social post.

---

## 15. Phasing / Roadmap (mapped to the sprint checkpoints)

- **Architecture Defense (24h):** research + this PRD + the plan; be able to defend user choice, verification
  placement, and trust boundaries.
- **MVP (Tue):** OpenEMR fork running locally + deployed publicly; full audit → `AUDIT.md`; `USERS.md`;
  `ARCHITECTURE.md` (agent plan). *"MVP is the foundation that makes a trustworthy agent possible,"* not a
  working agent.
- **Early Submission (Thu):** deployed agent; eval framework in place; observability wired in; demo video.
- **Final (Sun):** production-ready agent; cost analysis at 100/1K/10K/100K with architectural changes per
  tier; load/baseline results; demo video; social post.

### 15.1 Cost Economics & Scaling (AI Cost Analysis)
*(Home for the required AI Cost Analysis deliverable: actual dev spend + projected production cost at 100 /
1K / 10K / 100K users, with the architectural changes each tier forces. The case study is explicit that this
is **not** `cost-per-token × n users` — the per-user economics change as the architecture changes.)*

**Grounding rule.** Every number in the final analysis must be backed by **measured** cost-per-query from
observability (FR-OBS-2 tracks tokens + cost per request), not estimated in a vacuum. The model below is the
framework; the submission fills it with real telemetry.

**Per-query cost decomposition** (what actually drives spend):
- **LLM calls per query** — often **>1**: the agent turn(s) plus the verification pass (FR-VERIF-0), plus any
  tool-chaining reasoning steps. A single user question can be 2–4 model calls.
- **Input tokens per call** — system prompt + tool/JSON schemas (NFR-CONTRACT-1) + retrieved patient records
  + conversation history. In a record-grounded agent this dominates and grows with context.
- **Output tokens per call** — usually the smaller term.
- **Retrieval/embedding** — if semantic search over notes/documents is used.
- **Non-LLM infra** — agent compute, OpenEMR + DB, and **observability ingest** (trace/log volume has real
  cost at scale).

**Usage assumptions to fix (state them explicitly):** queries/clinician/day, active clinicians, avg tokens
per query, cache-hit rate, calls-per-query. *Illustrative anchor for the table below — replace with measured
values:* ~2.5 LLM calls/query, ~6k input + ~600 output tokens/call, before optimization.

**Dev spend (actuals).** Pull from the observability backend: experimentation, eval-suite runs (FR-EVAL —
these recur and add up), and load tests (NFR-PERF-4 at 10/50 users generate real token spend). Report the
actual dollar figure, not a guess.

**Tiered projection (structure to complete — architectural change is the point, numbers illustrative until
calibrated):**

| Users | Dominant cost driver | Architectural change that kicks in |
|---|---|---|
| **100** | Flat infra baseline dominates; LLM per-query minor | Single instance; managed LLM API; naive per-request retrieval is acceptable |
| **1K** | LLM per-query begins to dominate infra | Prompt/context minimization; **prompt & retrieval caching**; cache repeat lookups |
| **10K** | LLM spend material; observability ingest grows | **Model routing/tiering** (cheap model for simple lookups, premium only when needed); batching/async for pre-visit sweeps; committed-use LLM pricing; DB read replicas; **telemetry sampling** |
| **100K** | LLM + data-plane + egress; provider rate limits | Evaluate **self-hosting/fine-tuned small models** for common paths (find the crossover vs API); multi-region; horizontal autoscaling; dedicated caching tier; enterprise LLM contract; HIPAA-eligible host under BAA (§11) |

**Why per-user cost is non-linear (the levers):**
- **Caching** — prompt/context caching and repeat-query caching cut input-token cost sharply as volume rises.
- **Model routing** — most cardiology lookups are simple; routing them to a cheaper model reserves the premium
  model for genuine synthesis, bending the curve down.
- **Context minimization** — sending only the records a query needs (not the whole chart) is the single
  biggest per-query lever, since input tokens dominate.
- **Verification cost** — the trust core (FR-VERIF-*) adds LLM calls; a rules-based verifier where possible
  keeps groundedness without doubling model spend.
- **Volume/committed-use pricing** — provider discounts change unit cost at the top tiers.
- **Observability at scale** — trace/log ingest becomes a real line item; a sampling policy is a cost decision.
- **Self-host crossover** — at 100K there may be a point where a small hosted model beats per-token API cost
  for high-frequency simple queries; document where that crossover is, don't assume it.

**Tradeoff to state and defend:** cheaper (routing/caching/rules-based verification) vs. safest (premium model
+ LLM verification on every claim). In a clinical setting the floor is groundedness (M1) — cost optimizations
may **not** weaken source attribution or constraint enforcement. Document which levers you'd pull first and
which you'd refuse to.

---

## 16. Requirements Traceability Matrix

| Case-study "hard problem" | Requirements | Use cases | Gate deliverable |
|---|---|---|---|
| Authorization & access control | FR-AUTH-1..4, FR-CHAT-3, NFR-SEC-2 | UC-4 | ARCHITECTURE.md, AUDIT.md |
| Verification & trust | FR-VERIF-1..4, FR-DATA-1, FR-DATA-4 | UC-1,2,3 | ARCHITECTURE.md |
| Speed vs completeness | NFR-PERF-1..3, FR-CHAT-4 | UC-1,2 | ARCHITECTURE.md |
| Data security & HIPAA | NFR-SEC-1, FR-AUTH-4, §12 | UC-4 | AUDIT.md (compliance pass) |
| Failure modes | NFR-REL-1..2, FR-CHAT-4, FR-EVAL-1 | UC-5 | ARCHITECTURE.md, eval dataset |
| Observability (engineering) | FR-OBS-1..4 | all | dashboard + alerts |
| Evaluation (engineering) | FR-EVAL-1..4 | all | eval dataset |

---

## 17. Open Decisions (to resolve in ARCHITECTURE.md and defend in interview)

1. **Agent topology:** single agent vs multi-agent (e.g., a retriever + a verifier). Bias: start single,
   justify any split by a use case.
2. **LLM provider/model:** structured-output support, context window, cost/query, assumed-BAA. Decide and
   record rationale.
3. **Verification placement:** pre-generation constraint, post-generation claim-checking, or both — and
   exactly what each catches.
4. **Data path per type:** FHIR R4 vs read-only DB fallback, decided per data element with rationale.
5. **Memory/state model** for multi-turn context within a patient session.
6. **Domain-constraint source of truth:** where the cardiology rule set lives, how it's validated, and how
   its limits are documented.
7. ~~**Latency target number** for NFR-PERF-1, fixed during baselining.~~ **Resolved** — p95 ≤ 26s for a
   single-patient `RequestBrief` turn at up to 50 concurrent users, based on real measured load-test results.
   See `PERFORMANCE_BASELINES.md`.
8. **Production host decision** if Railway Enterprise BAA isn't pursued (§11).

---

*End of PRD v0.1. Next: expand §5 into `USERS.md` and §9–§11 into `ARCHITECTURE.md`.*
