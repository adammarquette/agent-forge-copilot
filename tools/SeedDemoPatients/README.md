# SeedDemoPatients

One-time operational script that seeds 20 synthetic demo patients into the QA OpenEMR instance
(GitLab issue #26 follow-up). Not part of the shipped product and not run by CI — `dotnet build`
and `dotnet format` cover it (kept clean like everything else in the solution), but the `unit-tests`
and `integration-tests` CI jobs filter by project filename and never invoke it.

## What this does — and doesn't — do

Creates 20 `Patient` FHIR resources (`POST /apis/{site}/fhir/Patient`) with obviously-synthetic
demographics (every given name is prefixed `"Demo "`, matching the existing "Ada Testpatient"
convention). **Patient demographics only** — no conditions, medications, or vitals.

This was originally scoped to also create cardiology-relevant clinical data for about half the
patients, but investigation found that's not achievable on this OpenEMR deployment via any API:

- Only `Patient` (plus `Practitioner`/`Organization`) has a working FHIR create route —
  `Observation`/`Condition`/`MedicationRequest` have none.
- `client_credentials` grants are hard-coded server-side to OpenEMR's "system" role, which this
  fork's scope generator never grants write access to for any resource — ruling out reusing the
  `client_credentials`/JWT-bearer system client built for issue #22.
- The legacy `/apis/{site}/api/...` REST API's write scopes for `medical_problem`/`prescription`/
  `vital` (which the checked-out source code defines) are rejected as `invalid_scope` when actually
  requested against the live deployed server — a real drift between source and deployment, not a
  naming mistake.

So this script closes part of issue #26 (a richer patient panel exists) but **not** the specific ask
of an INR lab on Ada Testpatient — no lab-creation path exists on this server at all. That issue
should stay open.

## Running it

```bash
dotnet run --project tools/SeedDemoPatients
```

Needs one interactive step: the script registers (or reuses, via `SeedDemo__ClientId`/
`SeedDemo__ClientSecret`) a confidential OAuth client, prints its client_id, and pauses for you to
enable it (Admin → System → API Clients) — freshly-registered clients land disabled. It then prints
an authorize URL; open it, log in as `admin`/`P@ssw0rd1`, approve, and paste back the resulting
address-bar URL (the redirect target doesn't resolve — that's expected, the `code` is still in the
URL). The access token this produces lives only in the process's memory — never logged, never
written to disk.

### Environment variables

| Variable | Purpose |
|---|---|
| `SeedDemo__BaseUrl` | OpenEMR base URL (defaults to the development Railway instance) |
| `SeedDemo__Site` | OpenEMR multi-site segment (defaults to `default`) |
| `SeedDemo__ClientId` / `SeedDemo__ClientSecret` | Reuse an already-registered, already-enabled client instead of registering a new one |
| `SeedDemo__AccessToken` | Skip the whole login flow entirely and use a token you already have (for quick reruns within that token's lifetime) |

## Output

Prints a per-patient `pid`/`uuid` table as it runs, a final summary, and a FHIR read-back
verification pass. Capture that table into the PR description for anyone who wants to reference the
seeded patients later — `OpenEmrQa__TestPatientId` (used by the integration test suite) is
deliberately left untouched by this script.
