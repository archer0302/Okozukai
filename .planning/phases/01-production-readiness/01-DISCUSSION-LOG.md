# Phase 1: Production Readiness - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-20
**Phase:** 1-Production Readiness
**Areas discussed:** Migration strategy, Proxy & TLS handling, Health check scope, Config & secrets shape

---

## Migration Strategy

### Q1 — How should migrations be applied when the API starts in Production?

| Option | Description | Selected |
|--------|-------------|----------|
| On API startup, always | Move `ApplyDatabaseMigrations()` out of the `IsDevelopment()` block. Zero new moving parts; the existing 10× retry loop handles a database not yet accepting connections; single-instance is a locked constraint. | ✓ |
| Separate one-shot migrator | A dedicated container/entrypoint runs to completion before the API starts, gated in compose. Cleaner separation, safer for multiple API instances. | |
| Startup, behind an env flag | Migrations run only when `RUN_MIGRATIONS=true`. Escape hatch for diagnosis, at the cost of a config knob and possible silent schema drift. | |

**User's choice:** On API startup, always

### Q2 — When migrations ultimately fail, what should the API do?

| Option | Description | Selected |
|--------|-------------|----------|
| Crash the process | Let the exception propagate (current behaviour after the retry loop). Container exits non-zero; restart policy retries the boot. | ✓ |
| Start anyway, report unhealthy | Keep the process alive and report unhealthy so logs stay reachable without a crash-loop. | |
| Crash, but widen the retry window | Same fail-fast behaviour with a longer retry/backoff than the current ~30s. | |

**User's choice:** Crash the process
**Notes:** Serving a ledger against a half-migrated schema is worse than being down.

### Q3 — Keep the `3D000` auto-create-database fallback in Production?

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it as a safety net | `POSTGRES_DB` normally creates the database, so the path shouldn't fire — but if it does, a fresh deployment comes up rather than crash-looping. Needs `CREATEDB` on the app role. | ✓ |
| Remove it — the DB is infra's job | Provisioning belongs to compose or a manual `createdb`; a missing database then fails loudly and the app role needs no `CREATEDB`. | |
| Keep, but Development only | Auto-create for local scratch databases, skipped in Production. | |

**User's choice:** Keep it as a safety net
**Notes:** The Development-only variant was rejected on the grounds that divergent boot paths between environments are the problem this phase exists to remove.

### Q4 — How should this phase prove Production mode works, given containers arrive in Phase 2?

| Option | Description | Selected |
|--------|-------------|----------|
| Manual run against a scratch DB | Empty local database, `ASPNETCORE_ENVIRONMENT=Production`, env-var config; confirm six migrations apply and no seed rows exist. | ✓ |
| Automated integration test | Production-configured `WebApplicationFactory` against a throwaway database. Repeatable, but the existing 22 integration tests don't spin up real PostgreSQL — likely means introducing Testcontainers. | |
| Both — manual now, test alongside | Manual boot as evidence plus a lighter automated check not needing a live database. | |

**User's choice:** Manual run against a scratch DB

---

## Proxy & TLS Handling

### Q1 — How to fix the `UseHttpsRedirection()` redirect loop?

| Option | Description | Selected |
|--------|-------------|----------|
| Drop HTTPS redirection entirely | The container only listens on plain HTTP internally; TLS terminates elsewhere or not at all. Removes the loop at its source with no forwarded-header trust config to get wrong. | ✓ |
| Add `UseForwardedHeaders`, keep redirection | Honour `X-Forwarded-Proto` so the app stops redirecting, preserving a real HTTPS guarantee. Needs `KnownProxies`/`KnownNetworks`, awkward with Docker's dynamic IPs. | |
| Both — forwarded headers, no redirect | Correct scheme/host for generated URLs and logs without the loop risk. | |

**User's choice:** Drop HTTPS redirection entirely

### Q2 — What sits in front of the stack on the homelab?

| Option | Description | Selected |
|--------|-------------|----------|
| Plain HTTP over Tailscale, no TLS | Tailscale encrypts the wire; the web entry point serves plain HTTP on the Tailnet with no TLS terminator. | ✓ |
| Tailscale Serve / HTTPS certs | Real HTTPS cert on the ts.net name, proxying to the container and forwarding `X-Forwarded-Proto`. | |
| A reverse proxy in the stack | nginx/Caddy/Traefik as the single published entry point, and the natural home for ORIG-03's single-origin routing. | |
| Not decided yet | Keep the API safe under any of them and settle topology in Phase 2/3. | |

**User's choice:** Plain HTTP over Tailscale, no TLS
**Notes:** This is the decision with the widest blast radius in the phase — it propagates into Phase 2's entry point and Phase 3's Tailnet wiring, and it removes any need for `UseForwardedHeaders`.

### Q3 — How should Kestrel bind in Production?

| Option | Description | Selected |
|--------|-------------|----------|
| HTTP only, via `ASPNETCORE_URLS` | Set from the environment (e.g. `http://+:8080`). No dev-cert or PFX plumbing; the port becomes part of the Phase 2 deployment contract. | ✓ |
| HTTP only, pinned in `appsettings.json` | A working committed default with no env var required — but cuts against PROD-05's "config from environment". | |
| Leave the default (port 8080) | The .NET container images already default to `http://+:8080`. Least code, but the binding is implicit and undocumented in the repo. | |

**User's choice:** HTTP only, via `ASPNETCORE_URLS`

---

## Health Check Scope

### Q1 — Should `/health` actually probe PostgreSQL?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — `/health` probes the DB, `/alive` stays liveness | Readiness/liveness split matching what compose and the container runtime consume. | ✓ (after correction) |
| No — keep it a bare liveness check | Simplest; migrations crashing on failure already implies a reachable database at boot. | |
| Yes, but one endpoint only | Single `/health` including the DB check, dropping `/alive`. | |

**User's choice:** Free-text — *"Is there still any value in the production? I don't think we have any dashboard looking at it."*, then *"okay yeah let's map them outside development"*

**Notes:** A two-step exchange worth preserving.

The user's challenge was fair: Grafana was removed precisely because nothing was watching it, and Docker does **not** restart containers on failing health checks — a failing check flips the status and nothing acts on it. The initial response conceded the point and recommended mapping the endpoints but skipping the database probe, on the stated grounds that the probe would cost "a package reference and DI wiring for a signal nobody consumes."

The user then asked what an Npgsql readiness probe actually is. Checking `src/Okozukai.Api/Okozukai.Api.csproj` and the package XML docs for `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.1.1 showed that `AddNpgsqlDbContext` "enables db context pooling, retries, **corresponding health check**, logging and telemetry", with `DisableHealthChecks` defaulting to `false`. The database health check is therefore *already registered in this app* — the stated cost did not exist, and that recommendation was withdrawn. The remaining value of the endpoints is narrow but real: compose startup ordering in Phase 2, `docker ps` at a glance, and PROD-04 itself.

### Q2 — How to satisfy PROD-04's "internal port, not public exposure"?

| Option | Description | Selected |
|--------|-------------|----------|
| Same port — the API port is never published | Rely on Phase 3's ACC-03: only the web entry point publishes to the host, so the API port exists only on the internal container network. | ✓ |
| Dedicated management port | Second Kestrel endpoint plus `RequireHost`. True defence in depth, at the cost of a second port in the deployment contract. | |
| Same port, excluded at the proxy | Entry point refuses to route `/health` onward — but there may be no proxy at all given the plain-HTTP decision. | |

**User's choice:** Same port — the API port is never published

### Q3 — Register a Docker `HEALTHCHECK` now, or expose only?

| Option | Description | Selected |
|--------|-------------|----------|
| Expose only — wiring is Phase 2's job | Endpoints respond outside Development; `HEALTHCHECK` and `depends_on: service_healthy` land with the container work. | ✓ |
| Note the contract for Phase 2 | Same, but record paths, port, and semantics in CONTEXT.md. | |
| Include a Dockerfile `HEALTHCHECK` now | Ships the healthcheck alongside the endpoints, but pulls PKG-01 forward. | |

**User's choice:** Expose only — wiring is Phase 2's job
**Notes:** The contract was recorded in CONTEXT.md anyway (D-12), since it costs nothing and saves Phase 2 rediscovering the tag split.

---

## Config & Secrets Shape

### Q1 — How should the connection string reach the API in Production?

| Option | Description | Selected |
|--------|-------------|----------|
| `ConnectionStrings__okozukai` env var | The env provider maps double-underscore to config sections, landing it where `AddNpgsqlDbContext("okozukai")` already looks. Zero code change. Password visible to `docker inspect`. | ✓ |
| Discrete `POSTGRES_*` vars, composed in code | One password variable feeds both the Postgres container and the API — at the cost of new startup code diverging from Aspire's expected shape. | |
| Docker secret via a `_FILE` variable | Password never appears in the process environment. Strongest for PKG-05, but needs custom config loading and compose secrets plumbing. | |

**User's choice:** `ConnectionStrings__okozukai` env var

### Q2 — What happens when the connection string is missing at startup?

| Option | Description | Selected |
|--------|-------------|----------|
| Fail fast with an explicit message | Validate at startup and name the missing key before EF/Npgsql produces an opaque error. | ✓ |
| Let Npgsql fail naturally | No extra code — but the error surfaces from inside the migration retry loop, so ten rounds of noise precede the real cause. | |
| Fail fast, and validate other required config too | Same guard extended to everything Production needs — risks failing on config that has sane defaults. | |

**User's choice:** Fail fast with an explicit message

### Q3 — What is the Aspire AppHost's role going forward?

| Option | Description | Selected |
|--------|-------------|----------|
| AppHost stays dev-only, untouched | Compose is the production runner; Aspire remains the local `aspire run` experience with user secrets and hardcoded Development. | ✓ |
| Also let AppHost read env config | Local dev and production configure the same way — convenience, not a requirement, since PROD-05 concerns the API. | |
| Plan to retire AppHost | Treat Aspire as a stepping stone. Cuts against the `Aspire.Hosting.JavaScript`/`PublishAsStaticWebsite` decision in PROJECT.md. | |

**User's choice:** AppHost stays dev-only, untouched

### Q4 — Where should Production environment values live for manual verification?

| Option | Description | Selected |
|--------|-------------|----------|
| Gitignored `.env`, committed `.env.example` | Documents every required variable, gives Phase 2's compose an `env_file` to consume, satisfies PKG-05. | ✓ |
| Exported shell variables only | Nothing to accidentally commit — but the required-variable list lives nowhere. | |
| Document in README, no files | Prose drifts from reality and compose can't consume it. | |

**User's choice:** Gitignored `.env`, committed `.env.example`
**Notes:** `.gitignore` currently has no `.env` entry; one must be added.

### Wrap-up — free-text raise

**User's response:** *"I want to make sure the setup fits to github action's deployment workflow."*, then *"the homelab I'm deploying to is also a self-hosted runner, so that wouldn't be an issue."*

CI/CD is Out of Scope for milestone 1 per REQUIREMENTS.md, so no workflow was folded into the phase. The compatibility check came back clean: `ConnectionStrings__okozukai` as an env var is exactly what a deploy workflow injects, `.env.example` is the contract naming the keys such a workflow must write, and fail-fast on missing config turns a forgotten secret into a loud failure. The one genuine constraint raised was reachability — a GitHub-hosted runner cannot reach a Tailnet-only box — which the user resolved by confirming the homelab box is itself a self-hosted runner. Recorded as a forward constraint for Phase 2 rather than as phase scope.

---

## Claude's Discretion

- Exact port number in `ASPNETCORE_URLS` — 8080 is the .NET container image default; nothing depends on it until Phase 2 fixes the deployment contract.
- Where the fail-fast connection-string guard lives — inline in `Program.cs` or an extension method alongside `AddInfrastructure`.
- Whether `appsettings.Production.json` is created at all, and Production log levels — surfaced but not discussed.

## Deferred Ideas

- **GitHub Actions deployment workflow** — Out of Scope for milestone 1. Forward constraint recorded for Phase 2: the homelab box is a self-hosted runner, so images build in place; no registry hop (GHCR optional for PKG-01) and no `tailscale/github-action` plumbing.
- **Narrowing `AllowedHosts` from `"*"`** — offered as a follow-up during the proxy discussion, not pursued. Low value while Tailnet-only.
- **`appsettings.Production.json` and Production log levels** — offered as a follow-up, not pursued.
