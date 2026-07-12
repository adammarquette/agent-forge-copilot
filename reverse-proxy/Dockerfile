# syntax=docker/dockerfile:1
# Same-origin reverse-proxy front door: stock nginx, no custom entrypoint. nginx's own image already runs
# docker-entrypoint.d/20-envsubst-on-templates.sh at container start, which
# envsubst's every *.template file under /etc/nginx/templates/ into
# /etc/nginx/conf.d/ - that's how OPENEMR_UPSTREAM/SIDECAR_UPSTREAM/PORT below
# reach the config without a custom image.
FROM nginx:1.27-alpine

COPY nginx.conf.template /etc/nginx/templates/default.conf.template

# Real service names in the "staging" Railway project this now deploys into
# (RAILWAY_TOKEN_RP is a copy of RAILWAY_TOKEN_STAGING) - confirmed against
# Railway's own service list, not guessed. Port 8080 matches the sidecar's
# own Dockerfile default (ASPNETCORE_URLS binds ${PORT:-8080}) but hasn't
# been confirmed as the actual private-networking port Railway assigned it -
# verify once this can actually be tested end to end.
ENV OPENEMR_UPSTREAM=openemr.railway.internal:80 \
    SIDECAR_UPSTREAM=agent-forge-api-staging.railway.internal:8080

# Also a placeholder - Railway's real internal DNS resolver address, once
# confirmed. 127.0.0.11 (Docker's own embedded DNS) only works for local
# docker-compose testing, not on Railway.
ENV DNS_RESOLVER=127.0.0.11

# Railway injects PORT at runtime; the template listens on it directly.
ENV PORT=8080
