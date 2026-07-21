# Observability stack (Epic 9 + Epic 107)

Self-hosted **Prometheus + Loki + Grafana**. Prometheus scrapes the sidecar's own OpenTelemetry
`/metrics` endpoint; Loki (Epic 107) ingests the sidecar's **logs** over OpenTelemetry's OTLP/HTTP
exporter; Grafana reads from both (`Program.cs`). ARCHITECTURE.md §11 / ENGINEERING_STANDARDS.md §7
keep the sidecar itself provider-agnostic - this folder is the one place specific backends are chosen,
and it's optional infra: the app boots and serves traffic without any of it (see
`ObservabilityHealthCheck`; the log exporter is fail-open - unset endpoint means console-only logging).

## Run it

```bash
# 1. Start the sidecar (separately) - the fixed local dev port is 5113:
dotnet run --project src/MarqSpec.AgentForge.Api

# 2. Start the observability stack:
docker compose -f observability/docker-compose.yml up
```

- Grafana: <http://localhost:3000> (`admin`/`admin` - change on first login). The **AgentForge
  Clinical Co-Pilot** dashboard is pre-provisioned (`grafana/dashboards/agentforge.json`):
  agent-turn rate, error rate, p50/p95 latency, tool-call rate + failure rate by tool,
  verification pass/fail rate, LLM tokens/cost, and a raw Polly (resilience/retry) panel.
- Prometheus: <http://localhost:9090> - scrapes `host.docker.internal:5113/metrics` every 15s
  (`prometheus/prometheus.yml`) and evaluates `alerts/agentforge-alerts.yml`.
- Loki: <http://localhost:3100> - query logs from **Grafana → Explore → Loki**, e.g.
  `{service_name="agentforge-api"}`. The sidecar pushes here automatically in Development
  (`appsettings.Development.json` sets `Observability:LokiOtlpEndpoint` to
  `http://localhost:3100/otlp/v1/logs`); if Loki isn't running, the sidecar just logs to console
  (fail-open). Config in `loki/loki-config.yaml`.

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
- **`agentforge-loki`** — **private only** (`agentforge-loki.railway.internal:3100`, no public domain; it
  has no auth of its own). Built from `loki/Dockerfile` (shares `loki/loki-config.yaml` with local compose;
  the image overrides the HTTP bind to `[::]` via a CLI flag, same IPv6-only reason as Prometheus). The
  sidecar pushes logs here via `Observability__LokiOtlpEndpoint` (set on the API service in
  `deploy.yml`); Grafana's `grafana/staging/loki-datasource.yml` overrides the Loki datasource to this
  private address. **One-time operator step:** attach a Railway volume at `/loki` so ingested logs survive
  restarts (like the sidecar's `/keys` volume) - not config-as-code.

All three build via Railway's `RAILWAY_DOCKERFILE_PATH` with the context at the repo root (same mechanism as
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
