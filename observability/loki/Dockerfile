# syntax=docker/dockerfile:1
# Staging Loki for the deployed sidecar (Epic 107, gitlab#107). Built via Railway's
# RAILWAY_DOCKERFILE_PATH service variable so the build context stays at the repo root -
# COPY path below is root-relative, exactly like observability/prometheus/Dockerfile.
#
# Railway private networking is IPv6-only: a service is only reachable at
# <name>.railway.internal if its process binds [::]. Loki defaults to 0.0.0.0 (IPv4), which
# the sidecar could never reach - so override the HTTP bind to [::] here. The config file is
# shared with local compose (which keeps the default bind); the flag wins over the file.
# Loki stays PRIVATE (no public domain) - it has no auth of its own (auth_enabled: false).
# reference: documentation/DEPLOYMENT_TOPOLOGY.md, gitlab#107
FROM grafana/loki:3.3.0

COPY observability/loki/loki-config.yaml /etc/loki/loki-config.yaml

ENTRYPOINT ["/usr/bin/loki"]
CMD ["-config.file=/etc/loki/loki-config.yaml", \
     "-server.http-listen-address=::"]
