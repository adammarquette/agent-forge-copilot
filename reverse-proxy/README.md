# reverse-proxy

Same-origin front door for the deferred reverse-proxy topology (`agent-forge#22`,
tracked in this repo as `#62`). A stock `nginx:1.27-alpine` container - no custom
code - routes:

- `/` -> OpenEMR
- `/agentforge/hubs/chat` -> the sidecar's SignalR hub (websocket upgrade)
- `/agentforge/` -> everything else the sidecar serves

This removes the cross-site condition behind `agent-forge#21` (OpenEMR's session
cookie not attaching after the launch iframe/tab traverses a third-party origin),
so the session cookie config on both sides can stay at their normal, unweakened
defaults (`SameSite=Lax`/`Strict`) instead of `SameSite=None`.

## Configuration

`OPENEMR_UPSTREAM` / `SIDECAR_UPSTREAM` (see `Dockerfile`) are the upstream host:port pairs the proxy
routes to. In the repo-root `docker-compose.yml` they are the compose service names —
`openemr:80` and `agent-forge-api:8080` — and in any other deployment they are whatever that
stack's private service DNS resolves to.

`DNS_RESOLVER` is the resolver nginx uses to look upstreams up **at request time** (rather than once
at startup), which is what lets the proxy boot before its upstreams exist and follow container
restarts without a reload. Under compose it is Docker's embedded DNS, `127.0.0.11`.

`PORT` is read at container start, so the listen port is set by the environment rather than baked
into the config — same convention as the main sidecar's own `Dockerfile`.

## Status

**Live** — this is the `reverse-proxy` service in the repo-root `docker-compose.yml`, and the **only
container in the stack that publishes a port** (`${DEMO_PORT:-8080}`). The full launch round-trip runs
through it: OpenEMR at `/`, the sidecar at `/agentforge/*` (with `Bff__PathBase=/agentforge` set on the
sidecar so its cookie path, SignalR URLs, and post-launch redirects all carry the prefix), and
`/agentforge/documents/` explicitly `404`ed so the ingestion endpoint is unreachable from outside the
network (W2-D17). Its config lint runs in `.github/workflows/ci.yml`.

## Dependencies

Both prerequisites have shipped:

- Sidecar path-base support (`#60` - `UsePathBase`, prefix-aware SignalR/chat URLs,
  cookie `Path=/agentforge`)
- OpenEMR-side config (`agent-forge#24`, `#25`, `#26` - redirect_uri, launch URI,
  `cookie_samesite` revert) - config only, no fork code changes

The per-environment values that still have to be set by hand (Site Address Override, the two OAuth
clients and their redirect URIs, the module's Launch URI) are in
[`../documentation/DEPLOYMENT.md`](../documentation/DEPLOYMENT.md) §4.
