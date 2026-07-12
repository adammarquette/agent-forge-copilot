# syntax=docker/dockerfile:1
# Same-origin reverse-proxy front door: stock nginx, no custom entrypoint. nginx's own image already runs
# docker-entrypoint.d/20-envsubst-on-templates.sh at container start, which
# envsubst's every *.template file under /etc/nginx/templates/ into
# /etc/nginx/conf.d/ - that's how OPENEMR_UPSTREAM/SIDECAR_UPSTREAM/PORT below
# reach the config without a custom image.
#
# Built via Railway's RAILWAY_DOCKERFILE_PATH service variable (set by
# gitlab_rp's deploy job), not a `railway up <subdirectory>` path argument -
# the latter hits a known Railway CLI "prefix not found" failure when a
# monorepo has more than one Dockerfile (confirmed live). RAILWAY_DOCKERFILE_PATH
# keeps the build context at the repo root, so COPY paths below are
# root-relative, not relative to this file's own directory.
FROM nginx:1.27-alpine

COPY reverse-proxy/nginx.conf.template /etc/nginx/templates/default.conf.template

# Real service names in the "staging" Railway project this now deploys into
# (RAILWAY_TOKEN_RP is a copy of RAILWAY_TOKEN_STAGING) - confirmed against
# Railway's own service list, not guessed. Port 80 for OpenEMR is confirmed
# against the fork's actual Dockerfile (Apache hardcoded to `Listen
# 0.0.0.0:80`, `EXPOSE 80 443` - no PORT-env override). Port 8080 for the
# sidecar matches its own Dockerfile default (ASPNETCORE_URLS binds
# ${PORT:-8080}) but hasn't been confirmed as the actual private-networking
# port Railway assigned it - verify once this can actually be tested end to
# end.
ENV OPENEMR_UPSTREAM=openemr.railway.internal:80 \
    SIDECAR_UPSTREAM=agent-forge-api-staging.railway.internal:8080

# Railway's real internal DNS resolver address - fixed, documented, and stable
# across every project/environment (not something to look up per-deployment):
# https://docs.railway.com/networking/private-networking/how-it-works. IPv6
# literal, so nginx's resolver directive needs it bracketed. For local
# docker-compose testing only, override to 127.0.0.11 (Docker's own embedded
# DNS) instead.
ENV DNS_RESOLVER=[fd12::10]

# Railway injects PORT at runtime; the template listens on it directly.
ENV PORT=8080
