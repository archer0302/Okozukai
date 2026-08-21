# Phase 2: Single-Origin Packaging - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-21
**Phase:** 2-Single-Origin Packaging
**Areas discussed:** Single-origin entry point

---

## Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| Single-origin entry point | What serves the one origin — separate proxy container vs the API serving the SPA | ✓ |
| Build & compose pipeline | Hand-written compose/Dockerfiles vs `aspire publish` generation; AppHost's role | |
| Dev-mode parity | How `aspire run` survives relative `/api` + CORS removal | |
| Caching & PWA updates | Cache headers and `registerType` behaviour (PKG-06) | |

**User's choice:** Single-origin entry point only.

---

## Single-Origin Entry Point

### Q1 — What serves the single origin?

| Option | Description | Selected |
|--------|-------------|----------|
| Separate web container | nginx/Caddy serves `dist/` and reverse-proxies `/api`; three services in compose | ✓ (later reversed) |
| API serves the SPA | `UseStaticFiles` + `MapFallbackToFile` in the API image; two services, no proxy config | |
| Aspire publish output | `AddViteApp` + `PublishAsStaticWebsite`; `aspire publish` emits compose | |

**User's choice:** Separate web container — **subsequently reversed, see Q5.**

### Q2 — Which web server?

| Option | Description | Selected |
|--------|-------------|----------|
| nginx | `nginx:alpine` + committed `nginx.conf`; verbose but explicit, abundant prior art | ✓ (moot after Q5) |
| Caddy | `caddy:alpine` + Caddyfile; terser, but automatic HTTPS is dead weight under D-07 | |
| You decide | Planner picks based on config clarity | |

**User's choice:** nginx. **Moot** — the reversal in Q5 removed the proxy container entirely.

### Q3 — What happens to `VITE_API_URL`?

| Option | Description | Selected |
|--------|-------------|----------|
| Delete it entirely | `baseURL` becomes relative; no env var, no fallback. Criterion 1 holds by construction. Requires a Vite dev proxy | ✓ |
| Keep as dev-only override | `VITE_API_URL \|\| ''`; prod relative, dev absolute; AppHost untouched | |
| You decide | Planner weighs safety against blast radius | |

**User's choice:** Delete it entirely.
**Notes:** Discovered during scouting that `journalService.ts` and `transactionService.ts` already send full `/api/...` paths, so `client.ts:4` is the only edit needed. Consequence accepted: `vite.config.ts` gains a `/api` dev proxy and the AppHost's `VITE_API_URL` injection is removed. → CONTEXT.md D-05, D-06.

### Q4 — Does the proxy pass through `/health` and `/alive`?

| Option | Description | Selected |
|--------|-------------|----------|
| Only `/api` | Health endpoints stay internal; closes T-01-02 by topology, per Phase 1 D-11 | ✓ |
| Proxy health too | Curl-able readiness from any Tailnet device; re-opens T-01-02 | |
| You decide | Planner chooses against Phase 3's Tailnet story | |

**User's choice:** Only `/api`.
**Notes:** Rendered moot by the Q5 reversal — with one app container the endpoints share the published port regardless. Re-examined and accepted rather than compensated for. → CONTEXT.md D-08.

### Q5 — Reversal: "one container for this app"

The user asked whether collapsing the whole app to a single container was achievable in this project or should be deferred.

**Assessment given:** achievable now and *less* work than the Q1/Q2 answer — it removes an image, a proxy config file, and a synchronisation point, while all five phase success criteria still hold. The distinction drawn was between one *app* container (viable) and one container including Postgres (pushed back on: hand-rolled multi-process image, ledger durability coupled to the app image lifecycle, no backups this milestone).

| Option | Description | Selected |
|--------|-------------|----------|
| One app container + Postgres | API serves the SPA from `wwwroot`; `postgres:17` separate with a named volume; two services | ✓ |
| Everything in one container | App and Postgres in one image under a supervisor | |
| Keep separate web container | Stay with the Q1 answer — nginx, three services | |

**User's choice:** One app container + Postgres.
**Notes:** This supersedes Q1 and Q2. → CONTEXT.md D-01, D-02, D-03.

### Q6 — Interjection: does any of this matter at this scale?

The user challenged the framing directly — homelab-only, Tailscale-gated, a journal app rather than real money management.

**Answer given, and recorded because it shapes planning:** the health-endpoint question is near-zero consequence and was framed against a Phase 1 threat record rather than a live risk. What does earn attention in this phase is the entry-point shape (costly to unwind), the `VITE_API_URL` deletion (ripples into AppHost and E2E), and above all **PKG-04's named volume** — the only item here that touches the project's core value, since the homelab disk is the sole copy of the ledger and backups are deferred. → CONTEXT.md `<specifics>`.

### Q7 — Wrap-up

| Option | Description | Selected |
|--------|-------------|----------|
| Write CONTEXT.md | Entry point settled; planner handles the rest with established defaults | ✓ |
| Cover the volume/data bits | PKG-04, `POSTGRES_DB` provisioning, `DevSeedData` wipe risk | |
| Cover dev-mode parity | Vite dev proxy, AppHost cleanup, Playwright impact | |

**User's choice:** Write CONTEXT.md.

---

## Claude's Discretion

- Dockerfile stage layout, base images, non-root user; hand-written Dockerfile vs `dotnet publish /t:PublishContainer`.
- Where the built SPA lands in the image and the exact middleware ordering around `UseStaticFiles` / `MapFallbackToFile`.
- Cache-header values and durations (PKG-06).
- Compose details: volume name, `env_file` wiring, restart policy, `depends_on` conditions.
- The `vite.config.ts` dev-proxy shape.
- Whether the app container gets its own `HEALTHCHECK`, and API-down behaviour — explicitly called low-stakes.

## Deferred Ideas

- Everything in one container including Postgres — rejected, recorded so it is not re-proposed.
- `RequireHost` / auth / management port for health endpoints — declined under D-08.
- Volume and data hazards (`POSTGRES_DB` provisioning vs the `3D000` fallback's `CREATEDB` assumption; `DevSeedData` wipe risk) — left to the planner; already tracked in STATE.md §Blockers/Concerns.
- `registerType: 'autoUpdate'` vs a prompted update — not selected for discussion; planner's discretion.
- GitHub Actions deployment workflow — carried forward from Phase 1, Out of Scope for milestone 1.
