# syntax=docker/dockerfile:1
# Same-origin reverse-proxy front door: stock nginx, no custom entrypoint. nginx's own image already runs
# docker-entrypoint.d/20-envsubst-on-templates.sh at container start, which
# envsubst's every *.template file under /etc/nginx/templates/ into
# /etc/nginx/conf.d/ - that's how OPENEMR_UPSTREAM/SIDECAR_UPSTREAM/PORT below
# reach the config without a custom image.
FROM nginx:1.27-alpine

COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# Sourced by the stock entrypoint before the envsubst step (see the script).
# Must be executable or the entrypoint skips it silently.
COPY 10-resolver.envsh /docker-entrypoint.d/10-resolver.envsh
RUN chmod +x /docker-entrypoint.d/10-resolver.envsh

# Defaults match the compose service names in the repo-root docker-compose.yml;
# override both for any other stack. Port 80 for OpenEMR is confirmed against
# the fork's actual Dockerfile (Apache hardcoded to `Listen 0.0.0.0:80`,
# `EXPOSE 80 443` - no PORT-env override). Port 8080 for the sidecar matches its
# own Dockerfile default (ASPNETCORE_URLS binds ${PORT:-8080}).
ENV OPENEMR_UPSTREAM=openemr:80 \
    SIDECAR_UPSTREAM=agent-forge-api:8080

# EMPTY BY DEFAULT, not 127.0.0.11: 10-resolver.envsh reads the real resolver out
# of the container's own /etc/resolv.conf at start, so one image works under
# Docker (embedded DNS) and on Railway (private networking) with no per-stack
# hardcoding. Set this to pin a resolver and skip detection; an IPv6 literal must
# be bracketed, because nginx's resolver directive requires it.
ENV DNS_RESOLVER=

# Resolve upstreams over IPv4 only. Railway private DNS is dual-stack (A + AAAA),
# but BOTH upstreams listen on IPv4 only - the sidecar's entrypoint binds
# 0.0.0.0 and the OpenEMR fork's Apache hardcodes `Listen 0.0.0.0:80` - so an
# AAAA answer yields a connection refused on an otherwise healthy stack. Harmless
# under Docker, whose embedded DNS returns A records anyway. Set to `on` only for
# a legacy IPv6-only Railway environment (pre-2025-10-16). reference: gh#140
ENV RESOLVER_IPV6=off

# Overridden at runtime by the environment; the template listens on it directly.
ENV PORT=8080
