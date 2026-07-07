# OpenEMR System Audit — AgentForge Clinical Co-Pilot

**Auditor:** Adam Marquette
**Date:** 2026-07-07
**Target:** OpenEMR fork (`agent-forge`), commit `ef3d490` — pruned base of OpenEMR master
**Environment audited:** Local `docker/development-easy` stack (app `https://localhost:9300`, MySQL/MariaDB `:8320`, phpMyAdmin `:8310`)
**Method:** Static analysis of the codebase and SQL schema, plus live probing of the running instance and demo database.

> **Scope note.** This audit covers the *base OpenEMR system as it exists today*, before any AI layer is added. Its purpose is to establish the ground truth an AI agent will build on: where the trust boundaries are, where the data lives, how fast it can be read, how reliable it is, and what compliance machinery already exists. Every finding is written to be traceable to a decision the Clinical Co-Pilot will have to make.

---

## 1. Executive Summary (Key Findings)

OpenEMR is a mature, security-aware EHR — not a toy. It ships parameterized queries (`QueryUtils`), AES-256-CBC+HMAC encryption (`CryptoGen`), a tamper-evident checksummed audit log, bcrypt password hashing, CSRF tokens, an ACL/RBAC engine (`AclMain` + `gacl`), and OAuth2/SMART-on-FHIR authorization on its REST surface. The agent should reuse this foundation, not reinvent it. The risks that matter cluster in three places: **(a) the dev deployment's insecure defaults, (b) config-gated safety controls, and (c) data gaps that will become agent hallucination vectors.**

**Most impactful — the deployment, not the code, is the exposure.** The `development-easy` compose ships hard-coded weak credentials (`admin`/`pass`, MySQL root `root`, CouchDB `password`) and publishes MySQL (`8320`), phpMyAdmin (`8310`), CouchDB (`5984`), Mailpit, OpenLDAP, and a VNC-accessible Selenium (`7900`) to host ports; the session cookie defaults to `cookie_secure=false`. If this stack becomes the Stage 2 "publicly accessible" deployment, the entire PHI store is reachable with known credentials over unencrypted side channels — the single largest HIPAA exposure. Harden before any public URL or real-looking data exists.

**Second — safety controls are opt-out, but the running instance has them ON.** Live verification confirms audit logging, forced breakglass logging, and strong passwords are enabled (`enable_auditlog=1`, `gbl_force_log_breakglass=1`, `secure_password=1`, bcrypt), with the log actively recording (18,884 rows, incl. 1,060 patient-record reads). Residual risk: these are toggleable, and breakglass users bypass capture. Critically, the base system will not auto-log a new agent service account's reads — the agent must call the disclosure/audit path explicitly, under the requesting clinician's identity.

**Third — the sample clinical data is not loaded, and the data model is fragmented.** Live queries show the database is effectively empty: **1 placeholder patient ("John Doe", blank race/ethnicity), and zero medications, problems, allergies, encounters, labs, or documents.** There is nothing for a co-pilot to read; loading realistic demo data is a prerequisite to Stages 4–5 and to any meaningful evaluation. Structurally, when data *is* loaded, it will be fragmented and weakly typed: medications live in **two** tables (`prescriptions` *and* `lists[type='medication']`); allergies and problems are also `lists` rows keyed only by `type`; nearly every field is nullable `varchar(255)` with no format constraint and minimal foreign keys; and codes (`rxnorm_drugcode`, `drug_id`, ICD) are commonly blank, leaving free text as the only signal. An agent that reads one medication source, or treats a free-text drug string as a coded fact, will confidently overstate what the record supports.

**Performance is fine for single-patient reads, weak for scans.** Core tables are InnoDB and `pid`-indexed, so "everything for one patient" — the agent's dominant pattern — is fast. But there is no app cache, no read replica, and no full-text index on notes; cross-patient or free-text search is slow and contends with clinical users on the one primary. Latency will be dominated by LLM round-trips, not OpenEMR reads.

**Architecture offers clean integration seams.** A Symfony `EventDispatcher` (hooks like `RestApiExtend`, `PatientReport`, `Services`), a PSR-4 service layer (`src/Services/*Service.php`) already encapsulating patient/med/lab reads, an OAuth2-secured REST+FHIR API (104 + 80 routes), and a first-class custom-module system all let the agent attach without patching core (§4.4 recommends running it as a separate FHIR/REST client with its own OAuth2 identity).

**Bottom line for the agent:** load real demo data first; reuse OpenEMR's auth/ACL/audit machinery; treat the two-source medication model and blank codes as first-class verification problems; keep audit + per-query disclosure logging on; and never deploy the dev compose as the public target.

---

## 2. Security Audit

### 2.1 Authentication
- **Password hashing is sound.** `src/Common/Auth/AuthHash.php` uses PHP `password_hash()` with the algorithm selectable in Admin → Globals → Security (`gbl_auth_hash_algo`), defaulting to `PASSWORD_DEFAULT` (bcrypt). Legacy `SHA512HASH` is remapped to the default. No home-grown crypto in the auth path.
- **Login hardening exists but is config-gated.** Strong-password enforcement (`secure_password`, default on), password history (`password_history`), expiration (`password_expiration_days`), grace period, and max-failed-login lockout (`password_max_failed_logins`) are all present as globals. MFA (`MfaUtils`) and Google sign-in are supported.
- **Risk — dev credentials.** The deployed dev stack sets `OE_PASS: pass` (user `admin`), MySQL root `root`, MySQL app `openemr`/`openemr`, CouchDB `password`. These are well-known and must be rotated before any public exposure. (`docker/development-easy/docker-compose.yml`.)

### 2.2 Authorization / Access Control
- **Real RBAC.** `AclMain`/`AclExtended` (backed by `gacl` / phpGACL) gate features by section+ACL. Access failures raise `AccessDeniedException`. This is the model the case study's "physician sees their patients, nurse differs, resident supervised" requirement should hook into.
- **Gap — access control is by *feature/section*, not strictly by patient panel.** OpenEMR does not, out of the box, enforce "this provider may only see patients assigned to them" for most reads; a user with the clinical ACL can generally open any patient. **This is the authorization boundary the agent must add**, not one it can assume. Do not let the agent inherit a blanket "read all patients" capability.
- **REST/API authorization** is enforced by an `AuthorizationListener` (Symfony subscriber) using OAuth2 Bearer tokens; only discovery/metadata routes (`/fhir/metadata`, `/api/version`, SMART config) use `SkipAuthorizationStrategy`. The "api" role is separated from the "users" role. This is the correct surface for the agent to consume.

### 2.3 Injection / Input Handling
- **SQL injection surface is well-managed in modern code.** `QueryUtils` and `sqlStatement*` use bound parameters; the service layer routes through them. `escapeTableName`/`escapeColumnName` guard identifier interpolation. Legacy `/library` and `/interface` procedural code still contains string-built queries in places — any of it the agent touches must be re-reviewed, not trusted by association.
- **CSRF** tokens are issued and verified via `CsrfUtils::collectCsrfToken`/`verifyCsrfToken` for form posts.

### 2.4 Session Security
- `SessionConfigurationBuilder` sets `cookie_httponly=true` and `cookie_samesite=Strict` (good) but **`cookie_secure=false` by default**; it is only raised to `true` in HTTPS/OAuth contexts. On the plain-HTTP `:8300` port, session cookies can traverse unencrypted.
- **Idle timeout defaults to 7200s (2 hours)** — long for a shared clinical workstation. Portal is 1800s. Consider shortening for the co-pilot's threat model (unattended terminal between rooms).

### 2.5 Data Exposure Vectors
- **PHI at rest:** encrypted blobs use AES-256-CBC + HMAC-SHA256 (`CryptoGen`), but **encryption keys are stored on the local filesystem** (`sites/default/documents/logs_and_misc/methods`) alongside the data they protect. Adequate for dev; for production, keys belong in a KMS/secret store, not on the same volume.
- **Side-channel services exposed by the dev compose** (phpMyAdmin, raw MySQL, CouchDB, LDAP, VNC) each bypass the application's ACL and audit layer entirely. Any PHI reachable through them is unlogged and unauthorized-by-app-standards.

---

## 3. Performance Audit

### 3.1 Storage engine & indexing
- Schema is **~282 tables, essentially all InnoDB** (2 stray `INNODB`/`InnoDb` casing variants, functionally identical) — transactional and row-locked, better than OpenEMR's old MyISAM reputation.
- Core clinical tables carry `pid`-based indexes: `patient_data` (5 keys), `lists` (4), `prescriptions` (3), `procedure_result` (3), `form_encounter` (4). **The agent's primary pattern — "load everything for one patient by pid" — is index-supported and cheap.**

### 3.2 Bottlenecks relevant to agent latency
- **No application cache.** There is no Redis/Memcached layer; reads hit MySQL directly every time. Repeated agent context-building for the same patient re-queries.
- **No read replica / connection pooling** in the dev stack — agent read load competes with live clinical writes on one primary.
- **No full-text index on clinical notes** (`form_*`, `lists.comments` longtext). Free-text search ("find where the patient reported chest pain") would be a slow `LIKE '%...%'` scan. Plan retrieval/embedding outside the primary DB.
- **Wide rows:** `patient_data` has ~132 columns, mostly `varchar(255)`. `SELECT *` pulls a lot of unused PHI; the agent should select only needed columns (also minimizes PHI in prompts).

### 3.3 Latency implication for the co-pilot
Single-patient synthesis (demographics + active meds + recent labs + problem/allergy lists) is a handful of indexed `pid` lookups — comfortably sub-100ms at the DB. The latency budget will be dominated by **LLM tool-call round-trips**, not OpenEMR reads. Design toward parallel tool fetches and caching the assembled patient snapshot per encounter.

---

## 4. Architecture Audit

### 4.1 Layering / where code lives
- `/src` — modern PSR-4 `OpenEMR\` namespace (~26 MB): services, FHIR, auth, crypto, events, REST controllers. **This is where new agent code belongs.**
- `/library` — legacy procedural PHP (~7.7 MB): `sql.inc.php`, `auth.inc.php`, globals. Still load-bearing; touch carefully.
- `/interface` — web UI controllers/templates (~70 MB), Smarty/Twig + Angular/jQuery.
- `/apis` — REST/FHIR dispatch entry (`apis/dispatch.php` → `ApiApplication`).
- `/sites/default` — per-site config, `sqlconf.php` (DB creds), document store, crypto keys.

### 4.2 Where clinical data lives (the agent's data map)
| Domain | Table(s) | Notes for agent |
|---|---|---|
| Demographics | `patient_data` (keyed by `pid`) | ~132 cols, weakly typed |
| Problems | `lists` where `type='medical_problem'` | shared table |
| Allergies | `lists` where `type='allergy'` | shared table |
| Medications | `prescriptions` **and** `lists` where `type='medication'` | **two sources — must reconcile** |
| Encounters | `form_encounter` + `forms` | encounter forms are pluggable |
| Labs / results | `procedure_order`, `procedure_result` | results normalized separately |
| Audit | `log` (checksummed) | tamper-evident |

### 4.3 Data-access layer
- Modern reads go through `src/Services/*Service.php` (e.g. `PatientService`, `PrescriptionService`, `EncounterService`, `ObservationLabService`), all extending `BaseService` and using `QueryUtils`. **Preferred agent tool target** — these already encapsulate joins, UUID handling, and some validation.

### 4.4 Integration points for the AI agent (ranked)
1. **Service layer (`src/Services`)** — call existing services as agent tools; inherits their query hygiene.
2. **REST + FHIR API** (104 standard + 80 FHIR routes, OAuth2-secured) — clean network boundary; lets the agent run as a separate process with its own credentialed identity and independent audit trail.
3. **Custom module** (`interface/modules/custom_modules/`) — first-class extension mechanism; the co-pilot UI can ship as a module (`oe-module-*` precedent exists).
4. **Event system** (`src/Events`, Symfony `EventDispatcher`) — hook points like `RestApiExtend`, `PatientReport`, `PatientSelect`, `Services`, `Main` for UI injection and behavior extension without core patches.

**Recommendation:** run the agent as a separate service that consumes the **FHIR/REST API under its own OAuth2 client**, and surface it in the UI via a **custom module**. This gives the agent a distinct, auditable identity and keeps it off the core PHP request path.

---

## 5. Data Quality Audit

> Live measurements against the running demo database are recorded in **§5.2**. The structural risks below come from the schema and are true regardless of row counts.

### 5.1 Structural data-quality risks (from schema)
- **Fragmented medications.** Two authoritative sources (`prescriptions`, `lists[type=medication]`) with no enforced link. An agent reading only one will under- or over-report meds.
- **Overloaded `lists` table.** Problems, allergies, medications, and more are distinguished only by a `type` string; a wrong/blank `type` silently misfiles clinical data.
- **Weak typing, no constraints.** Nearly all demographic/clinical columns are nullable `varchar(255)` with no format validation. `DOB` is nullable. Phones, sex, race/ethnicity are free strings.
- **Sparse coding.** `rxnorm_drugcode`, `drug_id`, and diagnosis codes are frequently empty in demo data — the record often has a free-text drug/problem name but no code, so "is the patient on drug X (by code)?" cannot be answered reliably from codes alone.
- **No DB-level referential integrity for most links.** Only ~47 FK-related declarations across 282 tables; `pid`/`provider_id`/`encounter` links are largely convention, not enforced — orphaned rows are possible.
- **Dual timestamps of unclear authority.** e.g. `prescriptions` has `date_added`, `date_modified`, `datetime`, `start_date`, `filled_date` — "most recent" is ambiguous; stale vs current must be defined explicitly.

### 5.2 Live demo-data measurements (running `openemr` DB, 2026-07-07)

Read-only queries were run against the live database via phpMyAdmin. **The headline result is that the realistic sample patient data required by MVP Stage 1 is not loaded.**

| Table | Rows |
|---|---|
| `patient_data` (patients) | **1** |
| `prescriptions` | 0 |
| `lists` (all types: meds/allergies/problems) | 0 |
| `form_encounter` (encounters) | 0 |
| `procedure_result` (labs) | 0 |
| `documents` | 0 |
| `forms` | 0 |
| `users` | 4 (1 human `admin`; 3 inactive service accounts) |
| `log` (audit) | 18,884 |

- The single patient is a hand-created placeholder — `pid=1`, "John Doe", `DOB=1985-06-07`, `sex=Male`, **`race` and `ethnicity` blank**, created `2026-07-07` (today). No meds, problems, allergies, encounters, labs, or documents attached.
- Only the `openemr` database exists (no alternate site/DB holding demo data).
- The audit log is healthy and actively recording: 18,884 rows spanning 2026-07-06 → 2026-07-07, 18 distinct event types, including **1,060 `patient-record-select`** and 7 `login` events — proof that audit capture works end-to-end.
- **Live config confirmed ON** in `globals`: `enable_auditlog=1`, `gbl_force_log_breakglass=1`, `secure_password=1`, `gbl_auth_hash_algo=DEFAULT` (bcrypt), `timeout=7200`, `portal_timeout=1800`.

**Implication:** there is effectively nothing for a Clinical Co-Pilot to read. Before Stages 4–5 and any agent evaluation are meaningful, load OpenEMR's demo/sample patient dataset (e.g. the official demo data, or synthetic patients via the FHIR API / `contrib` loaders). An agent evaluated against a one-patient, zero-record database cannot exercise the reconciliation, missing-data, and authorization edge cases the case study demands. This is now the top data-quality action item.

### 5.3 Agent failure modes implied
- Reporting an incomplete medication list (single-source read).
- Stating a med/problem as coded fact when only free text exists.
- Mis-aging or failing on patients with null `DOB`.
- Treating an orphaned/duplicate record as authoritative.

---

## 6. Compliance & Regulatory Audit (HIPAA)

### 6.1 Audit logging
- **Tamper-evident audit log exists.** `EventAuditLogger` writes to the `log` table with a per-row `checksum`; `auditSQLEvent` can log SQL-level events; disclosures are recorded via `recordDisclosure`. ATNA syslog export is supported (`enable_atna_audit`).
- **Verified ON in the running instance** (`enable_auditlog=1`, `gbl_force_log_breakglass=1`) with the log actively capturing patient-record reads (1,060 `patient-record-select` events already). Good baseline.
- **Config-gated and bypassable.** All audit capture keys can be disabled in globals, and "emergency"/breakglass users bypass logging unless `gbl_force_log_breakglass` is set (it currently is). **For the agent, these must stay pinned on, and every agent PHI read must call the disclosure/audit path under the requesting clinician's identity** — the base system will not auto-log a new external service account's reads.

### 6.2 Data retention
- OpenEMR provides audit and record history but **no built-in automated retention/purge policy** was found (no log-retention/purge routines in `src`/`library`). HIPAA expects a defined retention schedule (commonly 6 years for audit logs). This is a policy + implementation gap to close before production.

### 6.3 Breach notification
- No automated breach-detection/notification tooling in the base system. The checksummed audit log is the raw material for detecting tampering, but alerting/notification workflow must be built. Relevant to the agent's observability/alerting requirement.

### 6.4 BAA / sending PHI to an LLM provider
- **The central new compliance obligation.** Any PHI leaving OpenEMR for an LLM inference is a *disclosure* to a *Business Associate*. Requirements the ARCHITECTURE.md must satisfy: (1) a signed BAA with the LLM provider (per case-study instructions, assume one exists and that data is not used for training); (2) minimum-necessary — send only the fields required, not `SELECT *`; (3) log every disclosure to the LLM as an audit/disclosure event with correlation ID; (4) prefer de-identification or field-level minimization where the use case allows; (5) TLS in transit and no PHI in third-party logs/telemetry.
- **Demo data only.** Per project rules, only synthetic/demo data is used with this codebase — which keeps the audit itself out of real-PHI scope, but the architecture must be built as if the data were real.

---

## 7. Method, Evidence & Limitations

- **Static evidence:** codebase at commit `ef3d490`; `sql/database.sql` (282 tables); auth (`src/Common/Auth`), crypto (`src/Common/Crypto/CryptoGen.php`), audit (`src/Common/Logging/EventAuditLogger.php`), ACL (`src/Common/Acl`), REST auth (`src/RestControllers/Authorization`), globals (`library/globals.inc.php`), deployment (`docker/development-easy/docker-compose.yml`).
- **Live evidence:** running instance reachable at `https://localhost:9300` (self-signed cert) and phpMyAdmin at `:8310`. Demo-data measurements in §5.2 depend on authenticated DB access.
- **Limitations:** this audit reflects the base system pre-agent. It does not yet assess the agent's own code (none exists). Some legacy `/library` and `/interface` query paths were sampled, not exhaustively reviewed; anything the agent integrates with should be re-reviewed at integration time.
