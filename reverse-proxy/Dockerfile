# syntax=docker/dockerfile:1
# Same-origin reverse-proxy front door: stock nginx, no custom entrypoint. nginx's own image already runs
# docker-entrypoint.d/20-envsubst-on-templates.sh at container start, which
# envsubst's every *.template file under /etc/nginx/templates/ into
# /etc/nginx/conf.d/ - that's how OPENEMR_UPSTREAM/SIDECAR_UPSTREAM/PORT below
# reach the config without a custom image.
FROM nginx:1.27-alpine

COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# Defaults match the compose service names in the repo-root docker-compose.yml;
# override both for any other stack. Port 80 for OpenEMR is confirmed against
# the fork's actual Dockerfile (Apache hardcoded to `Listen 0.0.0.0:80`,
# `EXPOSE 80 443` - no PORT-env override). Port 8080 for the sidecar matches its
# own Dockerfile default (ASPNETCORE_URLS binds ${PORT:-8080}).
ENV OPENEMR_UPSTREAM=openemr:80 \
    SIDECAR_UPSTREAM=agent-forge-api:8080

# Docker's embedded DNS, which resolves sibling container/service names. Override
# for a stack whose resolver lives elsewhere; an IPv6 literal must be bracketed,
# because nginx's resolver directive requires it.
ENV DNS_RESOLVER=127.0.0.11

# Overridden at runtime by the environment; the template listens on it directly.
ENV PORT=8080
