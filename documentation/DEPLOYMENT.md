# DEPLOYMENT — the container stack

How AgentForge Clinical Copilot and its OpenEMR dependency are deployed, and what CI needs to keep it working.
**Everything runs as Docker containers**, and the reference deployment is the repo-root
[`docker-compose.yml`](../docker-compose.yml) stack — that is the supported way to stand this system up and see
it working end to end.

> **There is no hosted/public instance.** The project ran a managed-PaaS demo through mid-2026; it has been
> retired, and every environment (demo, QA, review) is now the same set of containers brought up locally or on
> whatever host the operator chooses. A Railway definition now exists in source (§9) but has **not** been
> applied, so this remains true today. Quick start is in the root [`README.md`](../README.md#run-it); the
> physical/network view is [`DEPLOYMENT_TOPOLOGY.md`](DEPLOYMENT_TOPOLOGY.md); this file is the operational
> runbook (bootstrap, config, quirks, rollback).

## 1. What runs

| Container (compose service) | Image / build | Role | Port |
|---|---|---|---|
| `reverse-proxy` | built from [`reverse-proxy/Dockerfile`](../reverse-proxy/) (nginx) | **The front door.** The single origin everything enters through — OpenEMR at `/`, the sidecar under `/agentforge`. | `${DEMO_PORT:-8080}` → published |
| `openemr` | **pulled** — `ghcr.io/adammarquette/agent-forge`, pinned to an immutable `sha-<12>` tag | OpenEMR EHR **with the `oe-module-agentforge` custom module and the fork's core launch patches baked in**. Not stock `openemr/openemr`. | 80 (internal) |
| `mysql` | `mysql:9.4` | OpenEMR's database. | 3306 (internal) |
| `agent-forge-api` *(profile `copilot`)* | built from the repo-root [`Dockerfile`](../Dockerfile); the same artifact is published as `ghcr.io/adammarquette/agent-forge-copilot` | The .NET 10 sidecar / BFF. | 8080 (internal) |
| `postgres` *(profile `copilot`)* | `pgvector/pgvector:pg17` | Week 2 data tier — hybrid-RAG corpus + `DerivedFactStore`. pgvector, not stock Postgres. | 5432 (internal) |

The observability containers (Prometheus, Loki, Grafana) live in a **separate** compose file,
[`observability/docker-compose.yml`](../observability/) — see [`observability/README.md`](../observability/README.md).

**Two tiers, by design.** `docker compose up -d` brings up OpenEMR + module + front door and needs **no secrets
at all**; `docker compose --profile copilot up -d` adds the sidecar and its Postgres, which need an Anthropic
key and a registered SMART client. The sidecar's `Llm:ApiKey` is `[Required]` with `ValidateOnStart`, so it
cannot boot without a real key — the profile split exists so the keyless tier doesn't crash-loop for anyone
who hasn't got one.

**No service publishes its own port.** Only the proxy is reachable from the host. That is load-bearing, not
cosmetic — see §2.

### Why OpenEMR is pulled, not built

The image is this project's **OpenEMR fork**, which carries core patches the SMART launch depends on
(`SessionUtil`'s launch-bridge cookie, `AuthorizationController`, `library/auth.inc.php`) on top of the
`oe-module-agentforge` module. Stock `openemr/openemr` with the module dropped on top does **not** reproduce a
working launch. The fork's own pipeline publishes the built image to GHCR on green `main`; building it here
would mean cloning a second repo and a ~10-minute Alpine/PHP build. To build from source anyway, see the
commented `build:` block on the `openemr` service in `docker-compose.yml`.

**Consequence: a change to the AgentForge OpenEMR module (e.g. `moduleConfig.php`, the launch pages) ships by
merging the fork and letting its CI republish the image — not by anything in this repo.** Then bump
`OPENEMR_IMAGE` (or the pin in `docker-compose.yml`) and recreate the container.

## 2. The one-origin invariant

Everything is reached through the one published origin (`http://localhost:8080` by default) — OpenEMR at `/`,
the sidecar under `/agentforge`. Two independent mechanisms break if that is violated:

1. **Session cookie.** A launch whose `/launch` and `/callback` land on different hosts loses its session
   cookie and fails with *"No pending SMART launch"*.
2. **OAuth `aud`.** OpenEMR's **Site Address Override** (`site_addr_oath`) must equal the sidecar's
   `OpenEmr__BaseUrl`. The sidecar builds the `aud` and the authorize URL from `BaseUrl`; OpenEMR validates
   `aud` against `site_addr_oath`. Any drift and every launch fails *"Aud parameter did not match authorized
   server"* before reaching a login form.

Generalized: **each environment's `OpenEmr__BaseUrl` must equal that environment's OpenEMR `site_addr_oath`,
and both must be the front door** — never a container's own hostname. Likewise, each launch flow's OAuth
client must be registered with **that flow's** redirect URI (`/agentforge/callback` for the patient launch,
`/agentforge/agenda/callback` for the roster/agenda launch).

## 3. Sidecar configuration

Options-pattern; `__` is the section separator. The compose file sets all of these — this table is the
reference for standing the sidecar up anywhere else.

| Variable | Value |
|---|---|
| `OpenEmr__BaseUrl` | the front door, e.g. `http://localhost:8080` — **must equal OpenEMR's `site_addr_oath`**, else `aud` mismatch |
| `OpenEmr__Site` | `default` |
| `Bff__PathBase` | `/agentforge` — the path the proxy reaches the sidecar under; drives `UsePathBase`, the session-cookie path, and the post-launch redirect prefix |
| `Bff__PublicBaseUrl` | front door + path base, e.g. `http://localhost:8080/agentforge`. Never the container's own hostname — that breaks the OAuth `redirect_uri` |
| `DataProtection__KeyRingPath` | `/keys`, backed by a **volume**. The cookie carrying the pending SMART launch is DataProtection-encrypted; an in-memory key ring can't decrypt it after a restart, which surfaces as "No pending SMART launch" on the callback |
| `OpenEmr__ClientId` / `OpenEmr__ClientSecret` | the patient-launch **confidential** SMART client, admin-enabled (redirect `/agentforge/callback`) |
| `OpenEmr__Scopes__0..14` | FHIR resource scopes — **casing matters**; `patient/encounter.read` is rejected. See `ServerScopeListEntity::fhirResourceScopesV1()` in the OpenEMR fork for the exact catalog. The list is 0-based and **contiguous** — a gap truncates the bound array at the first missing index |
| `OpenEmrAgenda__ClientId` / `OpenEmrAgenda__ClientSecret` | the roster/agenda OAuth client (redirect `/agentforge/agenda/callback`) |
| `OpenEmrAgenda__Scopes__0..5` | `openid`, `fhirUser`, `launch`, `api:fhir`, `user/Appointment.read`, `user/Patient.read` |
| `Llm__ApiKey` | Anthropic key. **Never in source** — supply from the environment / `.env` |
| `Llm__Model` | `claude-sonnet-5` |
| `Llm__InputPricePerMillionTokensUsd` / `Output…` | real per-million prices, so `agentforge_llm_cost_usd_total` reports actual cost rather than 0 |
| `AgentForgeData__ConnectionString` | Postgres/pgvector. Optional — the Week 2 flows are additive, and the host boots without it |
| `Observability__LokiOtlpEndpoint` | OTLP/HTTP log push. Fail-open: unset (or unreachable) means console-only logging |

**Local-development escape hatches.** `OpenEmr__AllowInsecureHttpForLocalDevelopment` and
`Bff__AllowInsecureHttpForLocalDevelopment` let the stack run over plain HTTP. They exist precisely for this
demo stack and are **not safe for anything else**.

## 4. First-run bootstrap (database state, scripted)

A fresh stack is a bare OpenEMR plus the module. The settings below are **live state in OpenEMR's database**,
not config-as-code, so they must be redone on any fresh stack or after a volume reset — but they are no longer
an Admin-GUI dance. Two tools do all of it, and both are idempotent:

```bash
# 1. the API half — registers both SMART clients, prints their ids/secrets
dotnet run --project tools/RegisterSmartClients -- http://localhost:8080

# put the printed ids/secrets in .env, then:

# 2. the database half — globals + enabling the clients
MYSQL_HOST=127.0.0.1 MYSQL_ROOT_PASS=<mysql root password> \
  dotnet run --project tools/BootstrapOpenEmr -- http://localhost:8080
```

`BootstrapOpenEmr` talks to MySQL directly (the settings have no API), reading the same environment variable
names the OpenEMR container uses: `MYSQL_HOST` (default `127.0.0.1`), `MYSQL_PORT` (`3306`), `MYSQL_DATABASE`
(`openemr`), `MYSQL_USER` (`root`), and `MYSQL_ROOT_PASS` / `MYSQL_PASS`. It waits for the schema (OpenEMR's
first boot takes minutes on a fresh volume), prints one line per setting — `ok` or `SET` — and refuses a
service-internal hostname, because that argument becomes `site_addr_oath` and §2's one-origin invariant rests
on it. Run it on every deploy if you like; a converged stack is all `ok`.

What the two tools write, and why each value matters:

1. **Site Address Override** (`site_addr_oath`) = the front door — *written by `BootstrapOpenEmr`*. It
   defaults to an *empty string*, which PHP's `??` does not treat as unset, so every OAuth URL the server
   computes comes out as a bare path with no scheme/host — which breaks both the admin "Register New App" GUI
   (`fetch()` on an unreachable URL — *"Failed to fetch"*) and JWT assertion audience validation
   (`invalid_client: Client authentication failed`) even with an otherwise correct assertion. The same tool
   also sets **"OAuth2 EHR-Launch Authorization Flow Skip"** (`oauth_ehr_launch_authorization_flow_skip`),
   the global that gates the per-client skip in step 2. Both are on Admin → Configuration → Connectors if you
   ever need to check them by eye.

2. **OAuth clients** — *registered by `RegisterSmartClients`, enabled by `BootstrapOpenEmr`*. Registration
   POSTs both clients against the **front door** with the correct scope lists, crucially including
   **`patient/Binary.read`** on the patient client. Without that scope on the *registered* client, OpenEMR's
   `finalizeScopes` silently drops the Binary scope the launch requests, the source-document fetch 401s, and
   click-to-source shows a misleading 404 (reference: gitlab#128). Freshly-registered clients then land
   **disabled** (the agenda client especially), so registration alone is not enough —

   ```sql
   UPDATE oauth_clients SET is_enabled = 1, skip_ehr_launch_authorization_flow = 1 WHERE client_id IN (…);
   ```

   is what `BootstrapOpenEmr` issues for every `AgentForge%` client. The **ids and secrets are the one thing
   neither tool can carry across** — they are generated at registration and consumed as sidecar config, so put
   them in `.env` as `OPENEMR_CLIENT_ID` / `OPENEMR_CLIENT_SECRET` and the agenda pair before bringing the
   `copilot` profile up.

3. **The AgentForge module's own settings** — *written by `BootstrapOpenEmr`*, as the four globals the
   module's `moduleConfig.php` page saves: **Launch URI** (`agentforge_launch_uri`) =
   `<front door>/agentforge/launch`, **Agenda Launch URI** (`agentforge_agenda_launch_uri`) =
   `<front door>/agentforge/agenda/launch`, **Issuer** (`agentforge_issuer`) =
   `<front door>/apis/default/fhir`, **Launch Mode** (`agentforge_launch_mode`) = `tab`. The per-patient and
   roster launches need **separate** URIs — the sidecar serves them from different paths, and one shared URI
   sends the per-patient "Launch AgentForge" button to the roster endpoint, which ignores patient context and
   renders the Daily Agenda instead of the patient chat. `tab` mode is required because the cross-origin
   iframe modal loses the session cookie. The tool writes these with the same upsert the fork's
   `AgentForgeGlobalConfig::save()` uses, so the result is indistinguishable from an admin saving that page.

4. **Demo data** — *still manual*. `admin` is the only login a fresh stack creates. The `cardio1` demo
   cardiologist and the demo patients are **not** built in — create the provider in Admin → Users and run
   `dotnet run --project tools/SeedDemoPatients`. Automating this into the compose bring-up is tracked in
   [#375](https://github.com/adammarquette/agent-forge-copilot/issues/375).

> **Not covered:** the document-ingestion cron's own globals (`agentforge_ingest_uri`,
> `agentforge_ingest_category_map`) are environment-specific — a private address and this install's document
> category ids — and are still saved from the module's config page.

> **Why the client is confidential, not public.** The architecture calls for a public client (D11 — no client
> secret held anywhere in the browser). Getting there required three real bugs to be found and fixed, in
> order: (1) `agent-forge` issue #11 — an operator-precedence bug in the fork's
> `TokenIntrospectionRestController.php` (`intval($x !== 1)` instead of `intval($x) !== 1`) that rejected
> introspection for enabled clients; (2)/(3) this repo's own `OpenEmrAuthClient` passed a null `ClientSecret`
> straight through to both `ExchangeAuthorizationCodeAsync` and `IntrospectAsync`, which Refit's `UrlEncoded`
> body serializer then omitted from the wire entirely — OpenEMR treats a missing `client_secret` field as an
> unauthenticated call and silently returns `{"active":false}`, even for a public client whose own registered
> secret is an empty string (issues #47, #52; both fixed — the null-to-`string.Empty` coercion is still
> correct and necessary). Even with all three fixed, a live end-to-end test against a properly-registered
> public/empty-secret client **still** failed identically, pointing at some remaining gap specific to how this
> OpenEMR fork handles an empty-secret client in the real request sequence — not reproducible in isolation,
> not root-caused. Switching to a confidential client (real secret) is what actually got the flow working;
> revisit the public-client path only if there's a concrete reason to (see issue #44's closing note for the
> full investigation trail).

## 5. Images & CI

Pipeline (`.github/workflows/ci.yml`): lint → build → test (unit + eval) → publish-image → verify.

- **`publish-image`** builds this repo's root `Dockerfile` **once** on merge to `main` and pushes
  `ghcr.io/adammarquette/agent-forge-copilot` (`sha-<12>` + `main` + `latest`) — the same build-once pattern
  the OpenEMR fork uses. "Build once, deploy that exact artifact." The image carries **no secrets**; runtime
  config comes from the environment (§3).
- **Pin, don't float.** Both images are pinned to an immutable `sha-<12>` tag rather than `:latest`, so a
  reviewer months from now gets the exact build this stack was verified against. `OPENEMR_IMAGE` in `.env`
  overrides the OpenEMR pin to follow `:latest`/`:main`.
- **Integration tests** run the BFF **in-process** on the CI runner and talk to an OpenEMR over FHIR/OAuth.
  They are **not** a deploy gate (#70): they drive a live browser login, so a transient OpenEMR outage must
  not veto a build. The job waits for OpenEMR to be healthy (a FHIR-metadata preflight) and skips with a
  distinct "dependency unavailable" signal if it never comes up, rather than failing the suite on login
  timeouts.

### Integration-test configuration

Supplied as GitHub Actions secrets/variables (see [`CI-SETUP.md`](CI-SETUP.md)):

- `OpenEmrQa__BaseUrl` — the front door of the OpenEMR the suite runs against (`http://localhost:8080` for a
  local compose stack)
- `OpenEmrQa__Site` = `default`
- `OpenEmrQa__TestPatientId` — a seeded demo patient's FHIR id. **These change on every reseed**, so treat the
  value as environment state, not a constant
- `OpenEmrQa__SecondTestPatientId` — a second seeded demo patient, for cross-identity scope tests
- `OpenEmrQa__TestAccessToken` — deliberately unset; when absent, `OpenEmrQaFixture` mints one itself via a
  Playwright login fallback (`tests/AgentForge.IntegrationTests/Support/OpenEmrQaFixture.cs`)
- `OpenEmrQa__System__ClientId`, `OpenEmrQa__System__PrivateKeyPath`, `OpenEmrQa__System__KeyId`,
  `OpenEmrQa__System__Scope` — the faster JWT-bearer path, see below
- `LlmQa__ApiKey`, `LlmQa__Model` — a real Anthropic key + model for test runs

> **"Unset" means DELETE the secret, not blank it.** The fixture/config code treats absent and whitespace-only
> identically (`IsNullOrWhiteSpace`), and the deliberately-optional values (`OpenEmrQa__TestAccessToken`,
> `OpenEmrQa__System__ClientId`/`PrivateKeyPath`) trigger their intended fallback paths only when genuinely not
> configured — so to represent unset, **delete the secret**, don't set it to an empty string.

> **Access-token expiry — solved.** OpenEMR access tokens live ~1 hour, so a *static*
> `OpenEmrQa__TestAccessToken` went stale between CI runs. The `password` grant was ruled out (OpenEMR only
> ever grants it identity-only scopes, never `api:fhir`, regardless of the user's ACL). The durable fix:
> `OpenEmrQaFixture` mints a fresh token itself, per test run, via `client_credentials` + a JWT-bearer client
> assertion (RFC 7523) — never expires in practice, no interactive browser flow. It needs:
>
> - A **confidential** OAuth client registered with `application_type: private`, a `jwks` containing an RS384
>   public key, `grant_types: ["client_credentials"]`, and `system/*` scopes. `token_endpoint_auth_method` can
>   be anything the registration endpoint accepts (`client_secret_post` works — this server's
>   `client_credentials` grant only ever authenticates via the JWT assertion regardless of the client's
>   recorded auth method). Self-service dynamic registration refuses this via the "Register New App" admin GUI
>   (it hardcodes `client_secret_post` and has no `grant_types` field) — register directly against
>   `POST /oauth2/{site}/registration` instead. The client lands **disabled**; one admin "Enable" click
>   (Admin → System → API Clients) is required before it can mint tokens.
> - The **"Enable OpenEMR FHIR System Scopes"** global (Admin → Configuration → Connectors,
>   `rest_system_scopes_api`) — off by default; without it every `system/*` scope is rejected as
>   `invalid_scope` regardless of naming.
> - The **"Site Address Override"** global set to the front door (§4.1).
>
> `PrivateKeyPath` expects a **file path** and GitHub Actions secrets are strings — store the PEM content as a
> secret and have the workflow write it to a temp file, then point `PrivateKeyPath` at it. When
> `System__ClientId`/`PrivateKeyPath` aren't set, the fixture falls back to the Playwright login path: slower
> and browser-dependent, but functionally sufficient.

## 6. Rolling forward and back

Because the sidecar ships as an immutable, CI-built image, "rollback" is a **tag change**, not a rebuild.

```bash
# roll forward / back: point at a known-good image and recreate
OPENEMR_IMAGE=ghcr.io/adammarquette/agent-forge:sha-<12> docker compose up -d --force-recreate openemr
docker compose --profile copilot up -d --build agent-forge-api      # or pull a pinned sidecar tag
```

- **From a known-good checkout:** `git checkout <last-known-good-sha>` and bring the stack up again.
- **From `main`, if the bad change is merged:** revert the bad commit(s) with a normal `git revert` (never
  `git reset --hard` on a shared branch) and push; CI republishes a fresh `sha-<12>`.
- **What a rollback does NOT touch:** the `openemr` and `mysql` containers and their volumes are independent of
  the sidecar, so a bad sidecar build never risks OpenEMR's data. Nor does it touch the **live OpenEMR state**
  from §4 (OAuth clients, globals, module settings) — that isn't versioned with the code. If the breakage is a
  bad *configuration* change rather than a bad *code* change, fix the configuration; rolling the image back
  won't help.

**Detecting the need to roll back:** watch `/agentforge/health` (process up) and `/agentforge/ready` (real
dependency checks) after any change. `/ready` failing where it previously passed is the signal — not a slow
first boot, which applies to OpenEMR (volume + setup step), not to the sidecar. CI's post-build smoke test
curls both, so a red smoke test is the signal without watching by hand.

**Only the health smoke test is a rollback signal — not the integration suite.** The integration job is
`allow_failure` and exercises an **external** OpenEMR via a live browser login. Red or skipped means the
OpenEMR under test was unavailable, or a real integration regression to *investigate* — it does not mean the
sidecar build is bad.

## 7. Known quirks

- **`SWARM_MODE=yes` is required on `openemr`.** Named volumes mount **empty** — they do not inherit image
  contents. The OpenEMR image only restores its `sites/` skeleton from `/swarm-pieces` (and then runs
  first-boot setup) when `SWARM_MODE=yes` and the replica elects itself leader. Without it the container
  crash-loops on a missing `sites/default/sqlconf.php`. Single replica ⇒ always leader ⇒ safe. Corollary:
  never run two overlapping `openemr` containers against the same volume — they race for leadership and the
  survivor waits ~10 minutes as a "follower".
- **OpenEMR first boot takes several minutes** (key generation + DB seed), and the container reports healthy
  before setup finishes. Watch the logs for *"Setup Complete!"* / *"Starting apache!"* before hitting the UI:
  `docker compose logs -f openemr`.
- **MySQL version.** OpenEMR officially targets MySQL 8.x; it runs clean against the 9.4 image here. If
  auth-plugin issues ever appear, pin MySQL to 8.4 and re-run setup with a fresh database + volume.
- **The proxy resolves upstreams at request time** (`resolver` + a variable `proxy_pass`), so it starts even
  when the sidecar is absent: `/` still serves OpenEMR and `/agentforge/` returns 502 until you add the
  `copilot` profile.
- **Demo patient ids change on reseed.** Anything pinned to a FHIR patient id (integration-test variables,
  saved requests) has to be refreshed after a reseed or a volume reset.

## 8. Production

Everything above is a **synthetic-data demo/QA posture**: plain HTTP, local-development escape hatches
enabled, throwaway credentials. A production deployment runs the **same container images** on a
HIPAA-eligible cloud under a signed BAA (default **AWS**), with TLS terminated at the edge, secrets from a real
secret store, and the Site-Address-Override / `aud` invariant of §2 pointed at the real front door. See
[`ARCHITECTURE.md`](ARCHITECTURE.md) §13 and D15.

## 9. Railway (Infrastructure as Code)

> **Status: defined, not yet applied.** `.railway/railway.ts` describes the stack, but no Railway project has
> been created from it. Until it is applied, §1's "there is no hosted/public instance" still holds — the
> compose stack remains the deployment. Tracked by labs.gauntletai.com#140.

Appended as §9 rather than inserted mid-document **on purpose**: the `§`-numbers are positional, and the
existing `reference: documentation/DEPLOYMENT.md §4` / `§7` comments (in `docker-compose.yml` and
`tools/RegisterSmartClients`, not `src/`) would silently point at the wrong section if everything below an
insertion shifted. See labs.gauntletai.com#141.

### Why Railway again, and why it is different this time

The previous managed environment was retired (commit `0a4c729`) because *"most of its working config only ever
existed as live dashboard edits"* — not because the platform was wrong. It could not be rebuilt from the
repository, so when it went away, it went away for good.

The whole point of this version is that **the definition lives in source**:

- [`.railway/railway.ts`](../.railway/railway.ts) is the project — five services, four volumes, every
  non-secret variable.
- [`.github/workflows/railway-config.yml`](../.github/workflows/railway-config.yml) plans on PR, applies on
  merge, and runs a **scheduled drift check** (`railway config plan --detailed-exit-code`, which exits `2`
  when the live environment no longer matches the file). A dashboard edit becomes a red build.

**Not `railway.json`/`railway.toml`.** Config as Code is deprecated, new services cannot opt into it, and
existing files stop being read on **2026-12-01**.

### The one manual step: generating the front door's domain

**Railway does not give a service a domain automatically, and IaC cannot create a generated one** — the docs
exclude `*.up.railway.app` domains from `.railway/railway.ts` in both `apply` and `pull` directions. So after
the first apply, someone must open the `reverse-proxy` service → **Settings → Networking → Generate Domain**.

Until that click happens, `${{reverse-proxy.RAILWAY_PUBLIC_DOMAIN}}` has no value, so `OpenEmr__BaseUrl` and
`Bff__PublicBaseUrl` are degenerate and **no SMART launch can work**. After it, **redeploy `agent-forge-api`**
so it picks the reference up — Railway resolves reference variables at deploy time, not continuously.

Be clear-eyed about what this costs: it is a **live dashboard edit**, the exact class of change that made the
previous environment unreproducible, and **the drift job cannot see it** because generated domains are outside
the planned graph. It is accepted deliberately as the cheap option (no DNS to own), and the mitigation is that
it is written down here rather than discovered. The alternative — `domains: ["<host>"]` on the proxy, deriving
the front door from that literal — moves the whole thing into source and under drift detection, at the cost of
owning DNS. Revisit if this environment becomes anything more than a demo.

### What the file cannot do

Applying it yields a stack that **boots but cannot complete a SMART launch** until §4's bootstrap runs.
Everything in §4 is live state in OpenEMR's *database* — Site Address Override, both OAuth clients, the
module's launch URIs, demo seeding — and no infrastructure tool can express it. The retired GitLab CI had
deploy-time self-heal jobs for exactly this (`a0dc176`, `60c969f`); that tree was deleted in `5a1be7b`.

**That gap is now closed in source rather than in CI:** `tools/RegisterSmartClients` (the API half) and
`tools/BootstrapOpenEmr` (the database half) are idempotent and environment-agnostic — they take the front
door as an argument, so the same two commands bootstrap compose, Railway, or anything else. A deploy job that
wants self-heal invokes them; nothing about them is CI-specific. What is left genuinely manual is feeding the
generated client ids/secrets back into the service variables, and demo seeding (#375) — which is why this
section still says "not yet applied" rather than "run this".

### Topology mapping

Compose service → Railway service, one for one. The invariants of §2 carry over unchanged:

| compose | Railway | notes |
|---|---|---|
| `reverse-proxy` | built from GitHub, `rootDirectory: reverse-proxy` | **the only service with a public domain** |
| `openemr` | pinned fork image | `SWARM_MODE=yes` still required — volumes mount empty (§7) |
| `mysql` | `mysql:9.4` image | image, not the managed helper, for parity with compose |
| `agent-forge-api` | pinned GHCR sidecar image | published by CI on merge to `main` (§5) |
| `postgres` | `pgvector/pgvector:pg17` | **must be pgvector**, not the managed Postgres helper |

`OpenEmr__BaseUrl` and `Bff__PublicBaseUrl` are derived from the proxy's own
`${{reverse-proxy.RAILWAY_PUBLIC_DOMAIN}}`, so the one-origin invariant holds through a domain change without a
hand edit. OpenEMR's `site_addr_oath` must still be set to that same value by the bootstrap — that half is
database state and cannot be derived.

### Two Railway-specific gotchas, both already handled

1. **DNS.** The proxy resolves upstreams at request time and needs a `resolver`. Docker's embedded DNS
   (`127.0.0.11`) does not exist on Railway, so `reverse-proxy/10-resolver.envsh` derives the resolver from the
   container's own `/etc/resolv.conf` at start. One image, both environments, nothing hardcoded. It is an
   `.envsh` because the stock nginx entrypoint *sources* those (and only executes `*.sh` in a subshell), so the
   export reaches the envsubst step that renders the template.
2. **IPv6.** Railway private DNS is dual-stack, but both upstreams listen on IPv4 only — the sidecar binds
   `0.0.0.0` and the fork's Apache hardcodes `Listen 0.0.0.0:80`. An AAAA answer would yield a connection
   refused on a perfectly healthy stack, so the resolver runs with `ipv6=off` (`RESOLVER_IPV6`, harmless under
   Docker). Set it to `on` only for a legacy IPv6-only Railway environment (pre-2025-10-16).

### Secrets

Never written to source. They must be set **once per environment** before the first deploy, or the sidecar
crash-loops on `Llm:ApiKey` (§3, `[Required]` + `ValidateOnStart`). **These are the Railway variable names, not
the compose `.env` names** — `OPENEMR_ADMIN_PASSWORD` and `ANTHROPIC_API_KEY` are `.env` inputs to
`docker-compose.yml` and mean nothing here.

**Environment-level SHARED variables** (one value, read by two services — a per-service secret could drift out
of sync, which compose made impossible by deriving both from one `.env` entry):

| variable | read by |
|---|---|
| `MYSQL_ROOT_PASSWORD` | `mysql` (`MYSQL_ROOT_PASSWORD`) + `openemr` (`MYSQL_ROOT_PASS`) |
| `MYSQL_PASSWORD` | `mysql` (`MYSQL_PASSWORD`) + `openemr` (`MYSQL_PASS`) |
| `POSTGRES_PASSWORD` | `postgres` + the sidecar's `AgentForgeData__ConnectionString` |

**Service-scoped `preserve()` variables** ("keep whatever is already set in Railway", so an apply never
clobbers them): `OE_PASS` on `openemr`; and on `agent-forge-api` — `Llm__ApiKey`, `OpenEmr__ClientId`,
`OpenEmr__ClientSecret`, `OpenEmrAgenda__ClientId`, `OpenEmrAgenda__ClientSecret`. The four OAuth values do
not exist until the §4 bootstrap has registered the clients.

> `preserve()` does not *supply* a value — it declines to manage one. On a first apply nothing is set, so the
> data services will not initialise and the sidecar will not boot until the variables above exist.

### Operating it

```bash
npm ci                      # installs the railway SDK so the file can be evaluated
npm run iac:typecheck       # tsc over .railway/railway.ts
railway login && railway link
# set the shared + preserve() variables above BEFORE the first apply, or the
# data services will not initialise and the sidecar will crash-loop
railway config plan         # preview; never mutates
railway config apply        # applies after confirmation
# then, IN THE DASHBOARD: reverse-proxy -> Settings -> Networking -> Generate Domain
# then redeploy agent-forge-api so it resolves RAILWAY_PUBLIC_DOMAIN
# then run the section 4 bootstrap against the new front door
```

CI needs a **project token** (scoped to one environment) as the `RAILWAY_TOKEN` repository secret.
