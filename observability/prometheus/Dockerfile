# syntax=docker/dockerfile:1
# Prometheus image for a DEPLOYED sidecar (gitlab#57) — as opposed to observability/docker-compose.yml,
# which mounts the local config and scrapes a `dotnet run` sidecar on the host. Build context is the repo
# root, so the COPY paths below are root-relative, exactly like reverse-proxy/Dockerfile.
# reference: documentation/DEPLOYMENT.md, gitlab#57
FROM prom/prometheus:v3.1.0

COPY observability/prometheus/prometheus.deployed.yml /etc/prometheus/prometheus.yml
COPY observability/alerts/agentforge-alerts.yml /etc/prometheus/alerts/agentforge-alerts.yml

# Bind [::] rather than the default 0.0.0.0:9090 (IPv4 only): some container networks resolve service
# names to IPv6 addresses, and an IPv4-only bind is unreachable there. Fixed :9090 keeps the Grafana
# datasource URL stable. This service must stay private — it has no auth of its own.
ENTRYPOINT ["/bin/prometheus"]
CMD ["--config.file=/etc/prometheus/prometheus.yml", \
     "--storage.tsdb.path=/prometheus", \
     "--web.console.libraries=/usr/share/prometheus/console_libraries", \
     "--web.console.templates=/usr/share/prometheus/consoles", \
     "--web.listen-address=[::]:9090"]
