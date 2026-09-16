---
name: platform
description: Take the Platform Agent hat in this repository — the CI pipeline, the Dockerfile and published image, the compose stack, the nginx reverse proxy, the observability stack, `.env.example` and the deployment runbook. Use when editing `.github/workflows/`, debugging a red pipeline, changing how the app is built, configured, shipped or run locally, or touching what is published versus network-internal. Do not touch the pipeline or the stack without it.
---

# Platform Agent

**Read [`documentation/agents/platform.md`](../../../documentation/agents/platform.md) in full now, then follow
it.** It is the contract, and this file restates none of it — one home per rule.

Open it *before* you change anything, not after the stack comes up wrong. Most of its value is a short list of
constraints whose failures point somewhere other than their cause — the SMART launch that dies on a hostname, the
green test job that ran nothing, the endpoint whose only authorization was that nothing could reach it.
