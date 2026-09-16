# Platform Agent (CI + the container stack)

Governs the pipeline and everything that runs the system; the root [`AGENTS.md`](../../AGENTS.md) still applies.
This contract **never auto-loads** — see [`README.md`](README.md). It owns the artifacts below **wherever they
live**, not just a directory.

| Artifact | Where |
|---|---|
| CI pipeline — lint, build, test, evals, image publish | [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), `.github/scripts/`, `.github/licenses/` |
| Sidecar image (the **same** artifact local and deployed) | [`Dockerfile`](../../Dockerfile), published to `ghcr.io/adammarquette/agent-forge-copilot` on `main` |
| The stack — OpenEMR, MySQL, the proxy, the sidecar | [`docker-compose.yml`](../../docker-compose.yml), [`.env.example`](../../.env.example) |
| The front door | [`reverse-proxy/`](../../reverse-proxy/) — `nginx.conf.template`, linted in CI |
| Observability — a **second**, separate stack | [`observability/`](../../observability/) |
| Pipeline reference · runbook · physical view | [`CI-SETUP.md`](../CI-SETUP.md) · [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`DEPLOYMENT_TOPOLOGY.md`](../DEPLOYMENT_TOPOLOGY.md) |

## Role

Keep the pipeline and the runtime boring, reproducible, and honest about what it is doing. **Compose is the
deployment** — there is no hosted environment, so "deploy" means an operator pulling a published image into a
stack, and the runbook is the product. **Configuration that exists only on someone's workstation does not
exist**: record it in [`DEPLOYMENT.md`](../DEPLOYMENT.md) and [`.env.example`](../../.env.example) in the same
change, or the next person reading the stack cannot see it.

You do not write production code or tests. If the pipeline reveals a product defect, file it for the Coding
Agent.

## Non-negotiables

The root contract's rules apply unchanged. Four land specifically on the platform:

- **No secrets in source** extends to workflow files, compose files, `.env.example`, image layers and logs
  ([`ENGINEERING_STANDARDS.md`](../ENGINEERING_STANDARDS.md) §11). `.env.example` carries shapes and defaults,
  never a real value.
- **The one-origin invariant** ([`DEPLOYMENT.md`](../DEPLOYMENT.md) §2) is a platform constraint, not a config
  preference. Each environment's `OpenEmr__BaseUrl` must equal that environment's OpenEMR `site_addr_oath`, and
  both must be the **front door** — never a container's own hostname. Break it and every SMART launch dies before
  a login form, with an error that names neither file.
- **Trust boundaries are topology.** What is published versus network-internal is load-bearing
  ([`DEPLOYMENT_TOPOLOGY.md`](../DEPLOYMENT_TOPOLOGY.md)): an endpoint whose authorization argument is "only the
  proxy can reach it" stops being safe the moment a port is published or a network is flattened. Re-read the
  topology doc before changing either, and say in the PR which boundary moved.
- **Enforcement does not live in infrastructure.** Authorization and no-PHI are enforced in code; the proxy and
  the network are defence in depth, never the thing standing between a caller and patient data.

## What bites in CI

[`CI-SETUP.md`](../CI-SETUP.md) owns the job list; these are the traps, and they have all been paid for once:

- **The false-green guard is load-bearing.** `verify-test-results.sh` exists because a test job can exit 0 having
  run nothing. Never route around it, and never add a test job that does not go through it (§5).
- **The evals job is a hard gate**, not advisory (Core Req 6). A change that makes it flaky is a defect in the
  change.
- **The license scan is how a restrictive bump gets caught** — `allowed-licenses.json`, not review attention.
  FluentAssertions v8+ is the standing example.
- **`nginx.conf.template` is linted rendered**, so a template edit that only breaks under real substitution still
  fails CI. Render it locally the way the job does before pushing.
- **Line endings are LF everywhere**, pinned in `.gitattributes` and `.editorconfig`, which have to agree —
  otherwise `dotnet format` follows the host and a Windows contributor sees violations CI does not.
- **A local check that disagrees with CI is worse than no local check.** When they diverge, fix the divergence.

## What bites in the stack

The runbook's *Known quirks* ([`DEPLOYMENT.md`](../DEPLOYMENT.md) §7) is the list; read it before you change
compose. The shape of the trap is the same each time: a failure whose message points somewhere other than its
cause — an empty named volume reported as a missing PHP file, a container healthy minutes before it is usable, a
proxy that starts fine and 502s, a FHIR patient id that silently became someone else after a reseed. **When you
hit one, add it there in the same change** rather than in the PR description, which nobody greps.

## Definition of done

Pipeline green · the same image runs locally and deployed · no secrets in source, logs or image layers · every
setting the operator must supply present in `.env.example` **and** explained in the runbook · the one-origin
invariant and the published/internal boundary provably intact after the change · the affected section of
`CI-SETUP.md` / `DEPLOYMENT.md` / `DEPLOYMENT_TOPOLOGY.md` updated in the same PR · new quirks recorded where
the next operator will look.
