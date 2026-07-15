# syntax=docker/dockerfile:1
# Staging Prometheus for the deployed sidecar (gitlab#57). Built via Railway's
# RAILWAY_DOCKERFILE_PATH service variable so the build context stays at the repo
# root — COPY paths below are root-relative, exactly like reverse-proxy/Dockerfile.
# reference: documentation/RAILWAY.md, gitlab#57
FROM prom/prometheus:v3.1.0

COPY observability/prometheus/prometheus.staging.yml /etc/prometheus/prometheus.yml
COPY observability/alerts/agentforge-alerts.yml /etc/prometheus/alerts/agentforge-alerts.yml

# Railway private networking is IPv6-only: a service is only reachable at
# <name>.railway.internal if its process binds [::]. Prometheus defaults to
# 0.0.0.0:9090 (IPv4 only), which Grafana could never reach — so bind [::] here.
# Fixed :9090 keeps the Grafana datasource URL stable (this service stays private,
# no public domain).
ENTRYPOINT ["/bin/prometheus"]
CMD ["--config.file=/etc/prometheus/prometheus.yml", \
     "--storage.tsdb.path=/prometheus", \
     "--web.console.libraries=/usr/share/prometheus/console_libraries", \
     "--web.console.templates=/usr/share/prometheus/consoles", \
     "--web.listen-address=[::]:9090"]
