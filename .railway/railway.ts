// -----------------------------------------------------------------------------
// Railway Infrastructure as Code — the hosted deployment of the AgentForge
// Clinical Copilot stack.
//
// THIS FILE IS THE DEPLOYMENT. The previous Railway environment was retired
// because "most of its working config only ever existed as live dashboard edits"
// (commit 0a4c729), so when it went down there was nothing to rebuild it from.
// Everything that can live in source lives here, and CI fails on drift between
// this file and the live environment.
//
// reference: labs.gauntletai.com#140, documentation/DEPLOYMENT.md
//
// NOT config-as-code: railway.json / railway.toml are deprecated, new services
// cannot opt into them, and they stop being read on 2026-12-01.
//
// WHAT THIS FILE CANNOT EXPRESS: the first-run bootstrap (DEPLOYMENT.md §4) is
// live state in OpenEMR's DATABASE - Site Address Override, both SMART OAuth
// clients, the module launch URIs, demo seeding. Applying this file yields a
// stack that boots but cannot complete a SMART launch until that bootstrap runs:
// tools/RegisterSmartClients (API half) then tools/BootstrapOpenEmr (database
// half), both idempotent and both taking the front door as their argument.
// -----------------------------------------------------------------------------

import { defineRailway, github, image, preserve, project, service, volume } from "railway/iac";

// Volumes are provisioned into a specific region; keep every volume in one
// region so private networking stays intra-region. Change in one place.
const REGION = "us-west2";

// Pinned, immutable tags - never `:latest`. Same rule as docker-compose.yml: a
// deployment is reproducible only if the tag cannot move under it. Bump
// deliberately; rollback is re-pinning the previous tag (DEPLOYMENT.md §6).
const OPENEMR_IMAGE = "ghcr.io/adammarquette/agent-forge:sha-dfe7e6c4077d";
const SIDECAR_IMAGE = "ghcr.io/adammarquette/agent-forge-copilot:sha-60c9495d39ff";

// Railway resolves ${{service.VAR}} references at deploy time. These are plain
// strings, not template literals - `${{` is literal text here.
const PROXY_DOMAIN = "${{reverse-proxy.RAILWAY_PUBLIC_DOMAIN}}";
const OPENEMR_HOST = "${{openemr.RAILWAY_PRIVATE_DOMAIN}}";
const SIDECAR_HOST = "${{agent-forge-api.RAILWAY_PRIVATE_DOMAIN}}";
const POSTGRES_HOST = "${{postgres.RAILWAY_PRIVATE_DOMAIN}}";
const MYSQL_HOST = "${{mysql.RAILWAY_PRIVATE_DOMAIN}}";

// The front door. LOAD-BEARING (DEPLOYMENT.md §2): this exact value must also be
// OpenEMR's `site_addr_oath`, or every SMART launch dies on "Aud parameter did
// not match authorized server" before a login form is reached. site_addr_oath is
// database state - the bootstrap sets it, this file cannot.
const FRONT_DOOR = "https://" + PROXY_DOMAIN;

export default defineRailway((ctx) => {
  // -------------------------------------------------------------------------
  // Volumes. Managed volumes mount EMPTY - see SWARM_MODE on openemr below.
  // -------------------------------------------------------------------------
  const mysqlData = volume("mysql-data", { region: REGION, sizeMB: 5120 });
  const openemrSites = volume("openemr-sites", { region: REGION, sizeMB: 2048 });
  const postgresData = volume("postgres-data", { region: REGION, sizeMB: 5120 });
  // Small, but NOT optional: the pending-SMART-launch cookie is DataProtection-
  // encrypted, and an in-memory key ring cannot decrypt it after a restart -
  // which surfaces as "No pending SMART launch" on the callback.
  const dataProtectionKeys = volume("dataprotection-keys", { region: REGION, sizeMB: 1024 });

  // -------------------------------------------------------------------------
  // Data tier. Pinned images rather than Railway's managed database helpers,
  // deliberately:
  //   - postgres MUST be pgvector (vector column + HNSW index; the EF migrations
  //     create the extension). The managed helper is stock Postgres.
  //   - mysql stays an image for parity with docker-compose.yml, so the OpenEMR
  //     fork sees identical MYSQL_* wiring in both environments.
  //
  // preserve() = "keep the value already set in Railway". Secrets are NEVER
  // written into this file; set them once per environment and every subsequent
  // apply leaves them alone. On a FIRST apply they are unset - see DEPLOYMENT.md.
  // -------------------------------------------------------------------------
  const mysql = service("mysql", {
    source: image("mysql:9.4"),
    env: {
      // Shared variables, NOT preserve(): mysql and openemr must hold the SAME
      // password, and two independent preserve() values can silently diverge -
      // compose derived both from one ${MYSQL_ROOT_PASSWORD}, and dropping to
      // per-service secrets would be a real regression in safety.
      MYSQL_ROOT_PASSWORD: ctx.shared.MYSQL_ROOT_PASSWORD,
      MYSQL_DATABASE: "openemr",
      MYSQL_USER: "openemr",
      MYSQL_PASSWORD: ctx.shared.MYSQL_PASSWORD,
    },
    volumeMounts: { "/var/lib/mysql": mysqlData },
  });

  const postgres = service("postgres", {
    source: image("pgvector/pgvector:pg17"),
    env: {
      POSTGRES_DB: "agentforge",
      POSTGRES_USER: "agentforge",
      // Shared, matching the sidecar connection string below. A service-scoped
      // preserve() here would be a DIFFERENT variable, and the sidecar could not connect.
      POSTGRES_PASSWORD: ctx.shared.POSTGRES_PASSWORD,
    },
    volumeMounts: { "/var/lib/postgresql/data": postgresData },
  });

  // -------------------------------------------------------------------------
  // OpenEMR - this project's FORK, not stock. Carries the core launch patches
  // (SessionUtil launch-bridge cookie, AuthorizationController, auth.inc.php)
  // on top of oe-module-agentforge. Stock openemr/openemr plus the module does
  // NOT reproduce a working launch. Published by the fork's own pipeline.
  // -------------------------------------------------------------------------
  const openemr = service("openemr", {
    source: image(OPENEMR_IMAGE),
    env: {
      MYSQL_HOST: MYSQL_HOST,
      MYSQL_PORT: "3306",
      // Same shared values the mysql service reads - see the note there.
      MYSQL_ROOT_PASS: ctx.shared.MYSQL_ROOT_PASSWORD,
      MYSQL_USER: "openemr",
      MYSQL_PASS: ctx.shared.MYSQL_PASSWORD,
      MYSQL_DATABASE: "openemr",
      OE_USER: "admin",
      OE_PASS: preserve(),
      // Volumes mount EMPTY - they do not inherit image contents. The OpenEMR
      // image only restores its sites/ skeleton from /swarm-pieces (and runs
      // first-boot setup) when SWARM_MODE=yes and the replica elects itself
      // leader; without this it crash-loops on a missing sites/default/sqlconf.php.
      // Single replica => always leader => safe. reference: DEPLOYMENT.md §7
      SWARM_MODE: "yes",
    },
    volumeMounts: { "/var/www/localhost/htdocs/openemr/sites": openemrSites },
  });

  // -------------------------------------------------------------------------
  // The .NET 10 sidecar / BFF. Pinned to the image CI publishes on merge to main
  // ("build once, deploy that exact artifact" - DEPLOYMENT.md §5).
  // -------------------------------------------------------------------------
  const sidecar = service("agent-forge-api", {
    source: image(SIDECAR_IMAGE),
    env: {
      PORT: "8080",

      // Both MUST be the front door, never a service's own hostname: BaseUrl
      // drives the SMART `aud` and the authorize URL; PublicBaseUrl drives the
      // OAuth redirect_uri. See DEPLOYMENT.md §2.
      OpenEmr__BaseUrl: FRONT_DOOR,
      Bff__PublicBaseUrl: FRONT_DOOR + "/agentforge",
      Bff__PathBase: "/agentforge",
      OpenEmr__Site: "default",

      // Railway terminates TLS at the edge, so the public origin is real HTTPS.
      // The AllowInsecureHttpForLocalDevelopment escape hatches the compose stack
      // needs are deliberately NOT set here.

      OpenEmr__ClientId: preserve(),
      OpenEmr__ClientSecret: preserve(),
      OpenEmrAgenda__ClientId: preserve(),
      OpenEmrAgenda__ClientSecret: preserve(),

      // The agenda/roster client has its OWN scope list and does not inherit the
      // patient list above - omitting it binds AgendaOpenEmrOptions with a null
      // Scopes and the roster launch cannot request anything. Same contiguity and
      // casing rules apply. reference: documentation/DEPLOYMENT.md section 3
      OpenEmrAgenda__Scopes__0: "openid",
      OpenEmrAgenda__Scopes__1: "fhirUser",
      OpenEmrAgenda__Scopes__2: "launch",
      OpenEmrAgenda__Scopes__3: "api:fhir",
      OpenEmrAgenda__Scopes__4: "user/Appointment.read",
      OpenEmrAgenda__Scopes__5: "user/Patient.read",

      // Contiguous 0-based list - a GAP SILENTLY TRUNCATES the bound array at the
      // first missing index. Casing matters: patient/encounter.read is rejected.
      // patient/Binary.read (index 9) is what makes click-to-source work: without
      // it on the REGISTERED client, OpenEMR's finalizeScopes drops it and the
      // document fetch 401s, surfacing as a misleading 404.
      OpenEmr__Scopes__0: "openid",
      OpenEmr__Scopes__1: "fhirUser",
      OpenEmr__Scopes__2: "launch",
      OpenEmr__Scopes__3: "launch/patient",
      OpenEmr__Scopes__4: "api:fhir",
      OpenEmr__Scopes__5: "patient/Patient.read",
      OpenEmr__Scopes__6: "patient/Encounter.read",
      OpenEmr__Scopes__7: "patient/Observation.read",
      OpenEmr__Scopes__8: "patient/DocumentReference.read",
      OpenEmr__Scopes__9: "patient/Binary.read",
      OpenEmr__Scopes__10: "patient/Condition.read",
      OpenEmr__Scopes__11: "patient/AllergyIntolerance.read",
      OpenEmr__Scopes__12: "patient/MedicationRequest.read",
      OpenEmr__Scopes__13: "patient/Procedure.read",
      OpenEmr__Scopes__14: "patient/DiagnosticReport.read",

      // [Required] + ValidateOnStart: the sidecar CANNOT boot without a real key.
      // Unset here is a crash-loop, not a degraded mode.
      Llm__ApiKey: preserve(),
      Llm__Model: "claude-sonnet-5",
      Llm__InputPricePerMillionTokensUsd: "3.00",
      Llm__OutputPricePerMillionTokensUsd: "15.00",

      // POSTGRES_PASSWORD is a shared variable on the environment, referenced so
      // the password lives in exactly one place for both postgres and the sidecar.
      AgentForgeData__ConnectionString:
        "Host=" + POSTGRES_HOST + ";Port=5432;Database=agentforge;Username=agentforge;Password=${{shared.POSTGRES_PASSWORD}}",

      DataProtection__KeyRingPath: "/keys",
    },
    volumeMounts: { "/keys": dataProtectionKeys },
  });

  // -------------------------------------------------------------------------
  // THE FRONT DOOR. The ONLY service with a public domain - the one-origin
  // invariant (DEPLOYMENT.md §2) is what makes the SMART launch work at all: a
  // launch whose /launch and /callback land on different hosts loses its session
  // cookie. Everything else is reachable only over private networking.
  //
  // Built from source (reverse-proxy/Dockerfile) rather than pulled: the image is
  // a few lines over stock nginx and is not published to any registry.
  // -------------------------------------------------------------------------
  const proxy = service("reverse-proxy", {
    source: github("adammarquette/agent-forge-copilot", {
      branch: "main",
      rootDirectory: "reverse-proxy",
    }),
    env: {
      // Railway injects PORT; the nginx template listens on it directly.
      OPENEMR_UPSTREAM: OPENEMR_HOST + ":80",
      SIDECAR_UPSTREAM: SIDECAR_HOST + ":8080",
      // Docker's embedded DNS (127.0.0.11) does not exist here. Left EMPTY on
      // purpose: reverse-proxy/10-resolver.envsh derives the real resolver from
      // the container's /etc/resolv.conf at start, which is correct on Railway
      // AND under Docker. Set a value only to override that detection.
      DNS_RESOLVER: "",
    },
    // NO `domains:` ENTRY, AND THAT IS A KNOWN GAP - not an oversight.
    //
    // Railway does not assign a domain automatically, and IaC cannot declare a
    // GENERATED *.up.railway.app domain (the docs exclude them from this file in
    // both apply and pull directions). So after a first apply someone must click
    // Settings -> Networking -> Generate Domain on this service, then redeploy
    // agent-forge-api so it resolves RAILWAY_PUBLIC_DOMAIN. Until then FRONT_DOOR
    // is degenerate and no SMART launch can work.
    //
    // That click is a live dashboard edit the drift job CANNOT detect, because
    // generated domains are outside the planned graph. Accepted deliberately as
    // the no-DNS option and written down in DEPLOYMENT.md §9. Declaring a custom
    // hostname here instead - `domains: [{ domain: "<host>", port: 8080 }]` with
    // FRONT_DOOR built from that literal - puts it back under source control and
    // drift detection, and requires re-running the §4 bootstrap so OpenEMR's
    // site_addr_oath matches.
  });

  return project("agent-forge-copilot", {
    resources: [
      mysql,
      postgres,
      openemr,
      sidecar,
      proxy,
      mysqlData,
      openemrSites,
      postgresData,
      dataProtectionKeys,
    ],
  });
});
