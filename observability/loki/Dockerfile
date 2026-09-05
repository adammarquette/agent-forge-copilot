# syntax=docker/dockerfile:1
# Loki image for a DEPLOYED sidecar (Epic 107, gitlab#107). Build context is the repo root, so the COPY
# path below is root-relative, exactly like observability/prometheus/Dockerfile.
#
# Bind [::] rather than Loki's default 0.0.0.0 (IPv4 only): some container networks resolve service names
# to IPv6 addresses, and an IPv4-only bind is unreachable there. The config file is shared with local
# compose (which keeps the default bind); the flag wins over the file. Loki must stay PRIVATE - it has no
# auth of its own (auth_enabled: false).
# reference: documentation/DEPLOYMENT_TOPOLOGY.md, gitlab#107
FROM grafana/loki:3.3.0

COPY observability/loki/loki-config.yaml /etc/loki/loki-config.yaml

# Run as root: the stock image runs as uid 10001, but hosts that mount the /loki volume root-owned leave
# Loki (10001) unable to create its storage dirs, crash-looping with "mkdir /loki/rules: permission
# denied". Root can write the volume. Acceptable for this private, synthetic-data-only service.
# reference: gitlab#107
USER root

ENTRYPOINT ["/usr/bin/loki"]
CMD ["-config.file=/etc/loki/loki-config.yaml", \
     "-server.http-listen-address=::"]
