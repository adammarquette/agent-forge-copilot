# syntax=docker/dockerfile:1
# Grafana image for a DEPLOYED sidecar (gitlab#57): the same AgentForge dashboard + datasources as local
# compose, pre-provisioned into the image. Build context = repo root, like reverse-proxy/Dockerfile.
#
# Admin credentials are NOT baked here — they come from the environment at runtime
# (GF_SECURITY_ADMIN_USER / GF_SECURITY_ADMIN_PASSWORD), and anonymous access is
# disabled (GF_AUTH_ANONYMOUS_ENABLED=false), so the dashboard requires a login.
# reference: documentation/DEPLOYMENT.md, gitlab#57
FROM grafana/grafana:11.4.0

COPY observability/grafana/dashboards /var/lib/grafana/dashboards
COPY observability/grafana/provisioning /etc/grafana/provisioning
# Replace the local-compose datasources with the deployed stack's service addresses. This image only;
# local compose (which mounts ../provisioning directly) is untouched.
COPY observability/grafana/deployed/datasource.yml /etc/grafana/provisioning/datasources/prometheus.yml
COPY observability/grafana/deployed/loki-datasource.yml /etc/grafana/provisioning/datasources/loki.yml
