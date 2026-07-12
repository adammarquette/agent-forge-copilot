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
hostnames (`<service-name>.railway.internal`). This deploys as a new sibling service in the
same "staging" Railway environment (project `lucid-clarity`) OpenEMR and the sidecar already
run in - one deployment, not a duplicate/parallel copy of the stack. The sidecar's hostname
is confirmed against Railway's real service list; OpenEMR's hostname and the actual port are
still placeholders pending confirmation.

`DNS_RESOLVER` defaults to Railway's real internal resolver (`[fd12::10]` - fixed and documented,
not per-deployment: see [Railway's private networking docs](https://docs.railway.com/networking/private-networking/how-it-works)).

`PORT` is Railway-injected at runtime, same convention as the main sidecar's own `Dockerfile`.

## Status

Not yet wired into production traffic. Deploys into the existing staging environment as a new
service (not a separate one), but the deploy job stays manual: OpenEMR's own config
(`agent-forge#24`, `#25`, `#26` - redirect_uri, launch URI, `cookie_samesite` revert) hasn't
been updated to route through this proxy yet, and the full launch round-trip and OpenEMR
regression pass (`agent-forge#28`, `#29`) haven't been verified against it.

## Dependencies

This only closes the loop once paired with:

- Sidecar path-base support (`#60` - `UsePathBase`, prefix-aware SignalR/chat URLs,
  cookie `Path=/agentforge`)
- OpenEMR-side config (`agent-forge#24`, `#25`, `#26` - redirect_uri, launch URI,
  `cookie_samesite` revert) - config only, no fork code changes
