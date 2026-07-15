# Observability stack (Epic 9)

Self-hosted Prometheus + Grafana, scraping the sidecar's own OpenTelemetry `/metrics` endpoint
(`Program.cs`). ARCHITECTURE.md §11 / ENGINEERING_STANDARDS.md §7 keep the sidecar itself
provider-agnostic - this folder is the one place a specific backend is chosen, and it's optional
infra: the app boots and serves traffic without it (see `ObservabilityHealthCheck`).

## Run it

```bash
# 1. Start the sidecar (separately) - the fixed local dev port is 5113:
dotnet run --project src/GauntletAI.AgentForge.Api

# 2. Start the observability stack:
docker compose -f observability/docker-compose.yml up
```

- Grafana: <http://localhost:3000> (`admin`/`admin` - change on first login). The **AgentForge
  Clinical Co-Pilot** dashboard is pre-provisioned (`grafana/dashboards/agentforge.json`):
  agent-turn rate, error rate, p50/p95 latency, tool-call rate + failure rate by tool,
  verification pass/fail rate, LLM tokens/cost, and a raw Polly (resilience/retry) panel.
- Prometheus: <http://localhost:9090> - scrapes `host.docker.internal:5113/metrics` every 15s
  (`prometheus/prometheus.yml`) and evaluates `alerts/agentforge-alerts.yml`.

Pointing at a deployed instance (e.g. Railway) instead of local `dotnet run`: edit the `targets`
list in `prometheus/prometheus.yml`.

## Staging (deployed on Railway)

The same stack runs on the `staging` environment (project `lucid-clarity`, issue #57) as two services
alongside the sidecar, so the panels are viewable without running anything locally:

- **`agentforge-grafana`** — public, login-gated. Credentials + URL are in the root
  [`README.md`](../README.md#observability-dashboard-grafana). Built from `grafana/Dockerfile` (bakes the
  same provisioning + dashboards; `grafana/staging/datasource.yml` overrides the datasource to the private
  Prometheus).
- **`agentforge-prometheus`** — **private only** (`agentforge-prometheus.railway.internal:9090`, no public
  domain). Built from `prometheus/Dockerfile` (bakes `prometheus/prometheus.staging.yml`, which scrapes
  `agent-forge-api-staging.railway.internal:8080/metrics` over the project's private network, and binds
  `[::]` because Railway private networking is IPv6-only).

Both build via Railway's `RAILWAY_DOCKERFILE_PATH` with the context at the repo root (same mechanism as
`reverse-proxy/`), and deploy from CI (`.gitlab/ci/deploy.yml`) using `RAILWAY_TOKEN_STAGING`. Grafana admin
credentials come from the `GF_SECURITY_ADMIN_*` Railway variables — never baked into the image.

## Alerts

`alerts/agentforge-alerts.yml` - 4 rules (FR-OBS-4 requires ≥3), each with a `summary` and a
`description` combining **meaning** (what firing means) and **on-call response** (what to check
first): `AgentForgeHighTurnLatencyP95`, `AgentForgeHighTurnErrorRate`,
`AgentForgeHighToolFailureRate`, `AgentForgeElevatedVerificationFailureRate`. This compose file
runs Prometheus's rule *evaluation* only - wiring a real Alertmanager (Slack/PagerDuty/email
routing) is a deployment-specific choice left to whoever stands this up for real, not hard-coded
here.

## Readiness

`GET /ready` on the sidecar checks OpenEMR FHIR, the LLM provider, and (if
`Observability__PrometheusHealthUrl` is configured, e.g.
`http://localhost:9090/-/healthy`) this Prometheus instance - `ObservabilityHealthCheck` reports
`Degraded` rather than an unconditional pass when that URL isn't set.
