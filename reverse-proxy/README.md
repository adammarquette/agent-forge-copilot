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

`OPENEMR_UPSTREAM` / `SIDECAR_UPSTREAM` (see `Dockerfile`) are Railway private-networking
hostnames (`<service-name>.railway.internal`) - set as real service variables once
OpenEMR and the sidecar exist as sibling services in whatever Railway environment this
deploys to. The defaults baked into the `Dockerfile` are placeholders, not confirmed values.

`PORT` is Railway-injected at runtime, same convention as the main sidecar's own `Dockerfile`.

## Status

Not yet wired into production traffic. Per `agent-forge-copilot#62`: deploys to a
**separate** Railway environment first; the current deployment is untouched until the
full launch round-trip and a full OpenEMR regression pass (`agent-forge#28`, `#29`) are
verified against it.

## Dependencies

This only closes the loop once paired with:

- Sidecar path-base support (`#60` - `UsePathBase`, prefix-aware SignalR/chat URLs,
  cookie `Path=/agentforge`)
- OpenEMR-side config (`agent-forge#24`, `#25`, `#26` - redirect_uri, launch URI,
  `cookie_samesite` revert) - config only, no fork code changes
