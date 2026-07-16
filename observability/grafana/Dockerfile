# syntax=docker/dockerfile:1
# Staging Grafana (gitlab#57): the same AgentForge dashboard + Prometheus datasource
# as local compose, pre-provisioned into the image. Built via RAILWAY_DOCKERFILE_PATH
# (build context = repo root), like reverse-proxy/Dockerfile.
#
# Admin credentials are NOT baked here — they come from Railway env at runtime
# (GF_SECURITY_ADMIN_USER / GF_SECURITY_ADMIN_PASSWORD), and anonymous access is
# disabled (GF_AUTH_ANONYMOUS_ENABLED=false), so the dashboard requires a login.
# reference: documentation/RAILWAY.md, gitlab#57
FROM grafana/grafana:11.4.0

COPY observability/grafana/dashboards /var/lib/grafana/dashboards
COPY observability/grafana/provisioning /etc/grafana/provisioning
# Replace the local-compose datasources (http://prometheus:9090, http://loki:3100) with the private
# staging addresses. Staging-only; local compose is untouched.
COPY observability/grafana/staging/datasource.yml /etc/grafana/provisioning/datasources/prometheus.yml
COPY observability/grafana/staging/loki-datasource.yml /etc/grafana/provisioning/datasources/loki.yml
