# syntax=docker/dockerfile:1
# Same-origin reverse-proxy front door (agent-forge#22, agent-forge-copilot#62):
# stock nginx, no custom entrypoint. nginx's own image already runs
# docker-entrypoint.d/20-envsubst-on-templates.sh at container start, which
# envsubst's every *.template file under /etc/nginx/templates/ into
# /etc/nginx/conf.d/ - that's how OPENEMR_UPSTREAM/SIDECAR_UPSTREAM/PORT below
# reach the config without a custom image.
FROM nginx:1.27-alpine

COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# Placeholders - replace with the real Railway private-networking hostnames
# once OpenEMR and the sidecar exist as sibling services in the environment
# this proxy deploys to (agent-forge-copilot#62's "separate Railway
# environment" note). Railway private networking hostnames are normally
# <service-name>.railway.internal.
ENV OPENEMR_UPSTREAM=openemr.railway.internal:80 \
    SIDECAR_UPSTREAM=agent-forge-api.railway.internal:8080

# Also a placeholder - Railway's real internal DNS resolver address, once
# confirmed. 127.0.0.11 (Docker's own embedded DNS) only works for local
# docker-compose testing, not on Railway.
ENV DNS_RESOLVER=127.0.0.11

# Railway injects PORT at runtime; the template listens on it directly.
ENV PORT=8080
