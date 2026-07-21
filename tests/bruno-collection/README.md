# AgentForge Bruno collection

FR-EVAL-4: a runnable API collection covering the sidecar's core HTTP endpoints, so a grader can
exercise real workflows without reading source.

## Running it

**Bruno app (GUI):** install [Bruno](https://www.usebruno.com/), "Open Collection", point it at
this folder, select the `local` environment, run requests individually or via the collection
runner.

**Bruno CLI (scriptable, CI-friendly):**

```
npx @usebruno/cli run --env local
```

Run from this directory (`tests/bruno-collection/`). Exits non-zero on any assertion failure -
directly usable as a single command producing pass/fail results (FR-EVAL-3).

## Prerequisites

The sidecar must be running locally first:

```
dotnet run --project src/MarqSpec.AgentForge.Api
```

This starts it on `http://localhost:5113` (the `local` environment's `baseUrl`), matching
`src/MarqSpec.AgentForge.Api/Properties/launchSettings.json`'s `http` profile.

## What's covered

- **Health/** - `/health` (liveness) and `/ready` (real dependency checks - PRD.md §13.1).
- **Launch/** - `/launch`, the SMART EHR launch redirect (INTERFACE_CONTROL.md A.3).
- **Metrics/** - `/metrics`, the Prometheus scrape endpoint (FR-OBS-3).
- **Chat/** - the SignalR chat hub's negotiate handshake.

## What's not covered, and why

The actual chat conversation (`/hubs/chat`) is a WebSocket-based SignalR protocol, not REST - a
plain HTTP collection can prove the hub is mapped and reachable (via negotiate) but can't drive a
real multi-message exchange the way `tests/MarqSpec.AgentForge.IntegrationTests/Api/*` already
does with a real `HubConnection`. The SMART launch's `/callback` step isn't included either: it
requires a real, live authorization code from an actual OpenEMR login, which can't be scripted
into a static collection request - see `documentation/AGENTS.md`'s note on the interactive SMART
flow, and `BffLaunchFlowTests`/`BffQaFixture.SeedAuthenticatedSessionAsync` for how the automated
test suite gets an authenticated session without a live browser login.
