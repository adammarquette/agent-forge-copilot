# MintQaIdentityToken

One-time operational script that mints a second, real patient-scoped OpenEMR access token for the QA
integration suite (GitLab issue #27). Not part of the shipped product and not run by CI - `dotnet build`
and `dotnet format` cover it, but the `unit-tests` and `integration-tests` CI jobs filter by project
filename and never invoke it.

## Why this exists

`Mcp.CrossIdentityAuthorizationTests` proves entitlement is per-identity, not shared - it needs two real,
distinct patient-scoped tokens (`OpenEmrQa__TestAccessToken` / `OpenEmrQa__SecondTestAccessToken`), each
from a separate SMART login. `client_credentials` (the durable fix for #22's token-expiry problem) mints a
token under OpenEMR's system role, which isn't tied to any patient - it can't stand in for either identity
here. This script performs a genuine `authorization_code` + PKCE **standalone launch** (`launch/patient` +
`patient/patient.read` scope), the same way the original `TestAccessToken` was obtained, and prints back
whatever patient context OpenEMR's own login/consent step resolves to - it does not assume or force which
patient gets selected.

Issue #26 seeded 20 synthetic demo patients into QA OpenEMR - log in as/select any one of those as identity
B (see MR !41's description for the full pid/uuid table). No need for a dedicated QA patient for this: the
test only needs two patients whose data don't leak into each other.

## Running it

```bash
dotnet run --project tools/MintQaIdentityToken
```

Needs one interactive step: the script registers (or reuses, via `MintToken__ClientId`/
`MintToken__ClientSecret`) a public OAuth client, prints its client_id, and pauses for you to enable it
(Admin -> System -> API Clients) - freshly-registered clients land disabled. It then prints an authorize
URL; open it, log in as (or select) the second, distinct patient identity, approve, and paste back the
resulting address-bar URL (the redirect target doesn't resolve - that's expected, the `code` is still in
the URL).

Before printing the values to paste into GitLab, it runs two checks against the real server using the
freshly minted token:
- **Self-check** - can the token read the patient it claims to be scoped to?
- **Isolation check** - if `OpenEmrQa__TestPatientId` is set in the environment, is the token genuinely
  blocked from reading identity A's patient? (Skipped, with a note, if that variable isn't set.)

The access token this produces lives only in the process's memory - never logged, never written to disk.

### Environment variables

| Variable | Purpose |
|---|---|
| `MintToken__BaseUrl` | OpenEMR base URL (defaults to the development Railway instance) |
| `MintToken__Site` | OpenEMR multi-site segment (defaults to `default`) |
| `MintToken__ClientId` / `MintToken__ClientSecret` | Reuse an already-registered, already-enabled client instead of registering a new one |
| `OpenEmrQa__TestPatientId` | Identity A's patient id, read only to run the isolation check above |

## Output

Prints the granted scope, token lifetime, and resolved `patient` claim, then the two values to paste into
GitLab CI/CD variables (Settings -> CI/CD -> Variables, protected + masked):

```
OpenEmrQa__SecondTestAccessToken = <token>
OpenEmrQa__SecondTestPatientId   = <patient uuid>
```
