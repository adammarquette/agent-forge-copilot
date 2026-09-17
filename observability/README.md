# Observability stack (Epic 9 + Epic 107)

Self-hosted **Prometheus + Loki + Grafana**. Prometheus scrapes the sidecar's own OpenTelemetry
`/metrics` endpoint; Loki (Epic 107) ingests the sidecar's **logs** over OpenTelemetry's OTLP/HTTP
exporter; Grafana reads from both (`Program.cs`). ARCHITECTURE.md §11 / ENGINEERING_STANDARDS.md §7
keep the sidecar itself provider-agnostic - this folder is the one place specific backends are chosen,
and it's optional infra: the app boots and serves traffic without any of it (see
`ObservabilityHealthCheck`; the log exporter is fail-open - unset endpoint means console-only logging).

## Run it

Which file you use depends on **where the sidecar runs**, because that decides whether Prometheus can
reach it. Picking the wrong one is not an error - it comes up clean and every dashboard panel is blank.

**A. Sidecar on the host (`dotnet run`)** - this folder's own stack, a separate compose project:

```bash
# 1. Start the sidecar (separately) - the fixed local dev port is 5113:
dotnet run --project src/AgentForge.Api

# 2. Start the observability stack:
docker compose -f observability/docker-compose.yml up
```

**B. Sidecar in a container (`--profile copilot`)** - the repo-root overlay, which runs these three
*inside* the main compose project so they share its network (see below):

```bash
docker compose -f docker-compose.yml -f docker-compose.observability.yml --profile copilot up -d
```

Either way Grafana is on <http://localhost:3000> with the same dashboard and datasources. **What feeds
them is not the same**, so each bullet below says which run mode it describes.

- Grafana (**A and B**): <http://localhost:3000> (`admin`/`admin` - change on first login). The
  **AgentForge Clinical Copilot** dashboard is pre-provisioned (`grafana/dashboards/agentforge.json`):
  agent-turn rate, error rate, p50/p95 latency, tool-call rate + failure rate by tool,
  verification pass/fail rate, LLM tokens/cost, and a raw Polly (resilience/retry) panel.
- Prometheus (**A and B**): <http://localhost:9090> - evaluates `alerts/agentforge-alerts.yml` in both
  modes. The scrape target is what differs: under **A** it scrapes `host.docker.internal:5113/metrics`
  every 15s (`prometheus/prometheus.yml`); under **B** the overlay mounts
  `prometheus/prometheus.deployed.yml` instead, whose target is `agent-forge-api:8080`. reference: #417
- Loki (**A and B**): <http://localhost:3100> - query logs from **Grafana → Explore → Loki**, e.g.
  `{service_name="agentforge-api"}`. How the sidecar reaches it is what differs: under **A** it pushes
  automatically in Development (`appsettings.Development.json` sets `Observability:LokiOtlpEndpoint` to
  `http://localhost:3100/otlp/v1/logs`); under **B** that same value would mean the sidecar's *own*
  container, so the overlay sets `Observability__LokiOtlpEndpoint=http://loki:3100/otlp/v1/logs` on
  `agent-forge-api` instead. Either way, if Loki isn't running the sidecar just logs to console
  (fail-open). Config in `loki/loki-config.yaml`.

## Pointing it at the containerized sidecar

The default `prometheus/prometheus.yml` scrapes `host.docker.internal:5113` — the sidecar run locally with
`dotnet run`. Against the sidecar **container** that target is dead, and this folder's compose file is a
*separate* compose project, so it lands on its own network and cannot resolve `agent-forge-api` at all.

Use [`../docker-compose.observability.yml`](../docker-compose.observability.yml) (run mode **B** above)
rather than editing anything. It is an overlay on the main stack, so the three services join that project's
network, and it closes both halves of the gap:

| Half | What the overlay does |
|---|---|
| Metrics | Mounts `prometheus/prometheus.deployed.yml` instead, whose target is `agent-forge-api:8080`. |
| Logs | Sets `Observability__LokiOtlpEndpoint=http://loki:3100/otlp/v1/logs` on `agent-forge-api`. Compose sets no such variable otherwise — only `appsettings.Development.json` does, and it points at `localhost:3100`, which **inside a container is the container itself**. |

Grafana's provisioned datasources already address `prometheus:9090` / `loki:3100` by service name, so the
shared network is all they need — no datasource edit either way.

## Deploying it alongside the sidecar

Each of the three has its own `Dockerfile` (`prometheus/`, `loki/`, `grafana/`) with the build context at the
repo root, so the same images that back this compose file deploy anywhere containers do — the compose file is
the reference wiring, not a special local-only mode. Two things travel with them wherever they go:

- **Grafana is the only surface that should ever be reachable.** Prometheus and Loki have **no auth of their
  own**; keep them on the private network. Grafana's admin credentials come from `GF_SECURITY_ADMIN_USER` /
  `GF_SECURITY_ADMIN_PASSWORD` in the environment — **never baked into the image**, and never left at the
  `admin`/`admin` default outside a local run.
- **Loki needs a durable mount at `/loki`** so ingested logs survive a container restart, the same way the
  sidecar needs one at `/keys`. The root overlay provides one (the `loki-data` volume); **this folder's
  compose file does not**, so a restart there discards whatever Loki had ingested.

See [`../documentation/DEPLOYMENT.md`](../documentation/DEPLOYMENT.md) for the stack this sits beside and
[`../documentation/DEPLOYMENT_TOPOLOGY.md`](../documentation/DEPLOYMENT_TOPOLOGY.md) for where it lands in the
network picture.

## Alerts

`alerts/agentforge-alerts.yml` - 5 rules (FR-OBS-4 requires ≥3), each with a `summary` and a
`description` combining **meaning** (what firing means) and **on-call response** (what to check
first): `AgentForgeHighTurnLatencyP95`, `AgentForgeHighTurnErrorRate`,
`AgentForgeHighToolFailureRate`, `AgentForgeElevatedVerificationFailureRate`,
`AgentForgeRetrievalDegradation`. This compose file
runs Prometheus's rule *evaluation* only - wiring a real Alertmanager (Slack/PagerDuty/email
routing) is a deployment-specific choice left to whoever stands this up for real, not hard-coded
here.

## Readiness

`GET /ready` on the sidecar checks OpenEMR FHIR, the LLM provider, and (if
`Observability__PrometheusHealthUrl` is configured, e.g.
`http://localhost:9090/-/healthy`) this Prometheus instance - `ObservabilityHealthCheck` reports
`Degraded` rather than an unconditional pass when that URL isn't set.
