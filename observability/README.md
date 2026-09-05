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
  Clinical Copilot** dashboard is pre-provisioned (`grafana/dashboards/agentforge.json`):
  agent-turn rate, error rate, p50/p95 latency, tool-call rate + failure rate by tool,
  verification pass/fail rate, LLM tokens/cost, and a raw Polly (resilience/retry) panel.
- Prometheus: <http://localhost:9090> - scrapes `host.docker.internal:5113/metrics` every 15s
  (`prometheus/prometheus.yml`) and evaluates `alerts/agentforge-alerts.yml`.
- Loki: <http://localhost:3100> - query logs from **Grafana → Explore → Loki**, e.g.
  `{service_name="agentforge-api"}`. The sidecar pushes here automatically in Development
  (`appsettings.Development.json` sets `Observability:LokiOtlpEndpoint` to
  `http://localhost:3100/otlp/v1/logs`); if Loki isn't running, the sidecar just logs to console
  (fail-open). Config in `loki/loki-config.yaml`.

## Pointing it at the containerized sidecar

The default `prometheus/prometheus.yml` scrapes `host.docker.internal:5113` — the sidecar run locally with
`dotnet run`. To watch the sidecar **container** from the main stack
([`../docker-compose.yml`](../docker-compose.yml), `--profile copilot`) instead, edit the `targets` list to
that container's address and put both stacks on the same Docker network. Set the sidecar's
`Observability__LokiOtlpEndpoint` to this Loki so its logs land here too.

## Deploying it alongside the sidecar

Each of the three has its own `Dockerfile` (`prometheus/`, `loki/`, `grafana/`) with the build context at the
repo root, so the same images that back this compose file deploy anywhere containers do — the compose file is
the reference wiring, not a special local-only mode. Two things travel with them wherever they go:

- **Grafana is the only surface that should ever be reachable.** Prometheus and Loki have **no auth of their
  own**; keep them on the private network. Grafana's admin credentials come from `GF_SECURITY_ADMIN_USER` /
  `GF_SECURITY_ADMIN_PASSWORD` in the environment — **never baked into the image**, and never left at the
  `admin`/`admin` default outside a local run.
- **Loki needs a durable mount at `/loki`** so ingested logs survive a container restart, the same way the
  sidecar needs one at `/keys`.

See [`../documentation/DEPLOYMENT.md`](../documentation/DEPLOYMENT.md) for the stack this sits beside and
[`../documentation/DEPLOYMENT_TOPOLOGY.md`](../documentation/DEPLOYMENT_TOPOLOGY.md) for where it lands in the
network picture.

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
