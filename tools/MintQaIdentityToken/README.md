# MintQaIdentityToken

One-time operational script that mints a real, patient-scoped OpenEMR access token for the QA integration
suite (GitLab issue #27). Not part of the shipped product and not run by CI - `dotnet build` and
`dotnet format` cover it, but the `unit-tests` and `integration-tests` CI jobs filter by project filename
and never invoke it.

## Why this exists

`Mcp.CrossIdentityAuthorizationTests` proves entitlement is per-identity, not shared - it needs **two**
real, distinct patient-scoped tokens, each from a separate SMART login:

- `OpenEmrQa__CrossIdentityTestAccessTokenA` - identity A, scoped to `OpenEmrQa__TestPatientId` (Ada
  Testpatient). **Not** the same as `OpenEmrQa__TestAccessToken`: once `SystemClientId` is configured
  (issue #22's fix), that one is a system-role `client_credentials` grant that can read every patient,
  which would make an isolation check against it meaningless.
- `OpenEmrQa__SecondTestAccessToken` / `OpenEmrQa__SecondTestPatientId` - identity B, scoped to a second,
  distinct patient.

`client_credentials` can't stand in for either - it isn't tied to any patient. This script performs a
genuine `authorization_code` + PKCE **standalone launch** (`launch/patient` scope, plus the FHIR resource
scopes `Mcp.GetPatientSummaryAsync` needs: `Patient`, `Condition`, `MedicationRequest`,
`AllergyIntolerance`, all PascalCase - confirmed against the live server's
`.well-known/openid-configuration`, which rejects the lowercase form as `invalid_scope`), the same way the
original `TestAccessToken` was obtained. It prints back whatever patient context OpenEMR's own
login/consent step resolves to - it does not assume or force which patient gets selected.

Run it **twice**: once logging in as/selecting Ada Testpatient for identity A, once for a second, distinct
patient for identity B. Issue #26 seeded 20 synthetic demo patients into QA OpenEMR - use any one of those
for identity B (see MR !41's description for the full pid/uuid table). No need for a dedicated QA patient:
the test only needs two patients whose data don't leak into each other.

## Running it

```bash
dotnet run --project tools/MintQaIdentityToken
```

Needs one interactive step: the script registers (or reuses, via `MintToken__ClientId`/
`MintToken__ClientSecret`) a public OAuth client, prints its client_id, and pauses for you to enable it
(Admin -> System -> API Clients) - freshly-registered clients land disabled. It then prints an authorize
URL; open it, log in as (or select) the target patient identity, approve, and paste back the resulting
address-bar URL (the redirect target doesn't resolve - that's expected, the `code` is still in the URL).

Before printing the values to paste into GitLab, it runs two checks against the real server using the
freshly minted token:
- **Self-check** - can the token read the patient it claims to be scoped to?
- **Isolation check** - if `OpenEmrQa__TestPatientId` is set in the environment and differs from the
  minted token's own patient, is the token genuinely blocked from reading it? (Skipped, with a note,
  otherwise - e.g. this is naturally skipped on the run where you mint identity A's own token.)

The access token this produces lives only in the process's memory - never logged, never written to disk.

### Environment variables

| Variable | Purpose |
|---|---|
| `MintToken__BaseUrl` | OpenEMR base URL (defaults to the development Railway instance) |
| `MintToken__Site` | OpenEMR multi-site segment (defaults to `default`) |
| `MintToken__ClientId` / `MintToken__ClientSecret` | Reuse an already-registered, already-enabled client instead of registering a new one |
| `OpenEmrQa__TestPatientId` | Identity A's patient id, read only to run the isolation check above |

## Output

Prints the granted scope, token lifetime, and resolved `patient` claim, then which pair of GitLab CI/CD
variables to paste it into (Settings -> CI/CD -> Variables, protected + masked) - depending on which
patient you selected:

```
# If the resolved patient matches OpenEmrQa__TestPatientId (identity A):
OpenEmrQa__CrossIdentityTestAccessTokenA = <token>

# Otherwise (identity B):
OpenEmrQa__SecondTestAccessToken = <token>
OpenEmrQa__SecondTestPatientId   = <patient uuid>
```
