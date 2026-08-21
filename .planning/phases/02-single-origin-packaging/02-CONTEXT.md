# Phase 2: Single-Origin Packaging - Context

**Gathered:** 2026-08-21
**Status:** Ready for planning

<domain>
## Phase Boundary

The frontend and API build into container images that a single command brings up as one stack, with the browser talking to exactly one origin instead of two. Covers ORIG-01 (relative API path), ORIG-02 (CORS removal), ORIG-03 (single origin serving both SPA and `/api`), and PKG-01 through PKG-06 (API image, production frontend build, one-command stack, named volume, runtime secrets, cache headers).

**In scope:** `src/Okozukai.Api/Program.cs` (CORS removal, static-file serving, SPA fallback), `src/Okozukai.Frontend/src/api/client.ts` (relative base URL), `src/Okozukai.Frontend/vite.config.ts` (dev proxy), `src/Okozukai.AppHost/Program.cs` (removal of `VITE_API_URL` injection), a new Dockerfile for the combined app image, `.dockerignore`, and a committed `compose.yaml`.

**Out of scope (later phases):** Tailscale wiring, Tailnet address, and port-publishing policy (ACC-01/02/03, Phase 3); query indexes (PERF-01/02, Phase 4); CI/CD deployment automation and backups (Out of Scope for milestone 1).

</domain>

<decisions>
## Implementation Decisions

### Single-Origin Entry Point (ORIG-03, PKG-01, PKG-02)

- **D-01:** **The app runs as one container.** The API image serves the built Vue SPA itself — `app.UseStaticFiles()` plus `app.MapFallbackToFile("index.html")` after `app.MapControllers()` in `src/Okozukai.Api/Program.cs`. There is **no nginx or Caddy container** and no reverse-proxy config file anywhere in the repo. Routing needs no rewriting: every controller is already `[Route("api/[controller]")]` and every frontend service call already sends a full `/api/...` path, so `/api/*` hits controllers and everything else falls through to the SPA. — **Reversibility:** costly — undoing this means authoring a proxy config, adding a third service to compose, splitting the Dockerfile into two images, and revisiting Phase 3's port-publishing wiring.

  *Decision history:* a separate nginx container was chosen first and then deliberately reversed when the user asked to simplify to a single container. The reversal is a net reduction in work, not a compromise: it removes an image, a config file, and a synchronisation point. Downstream agents should **not** re-propose a proxy container.

  *Accepted cost:* a frontend-only change rebuilds the .NET image. On a box that builds locally this is rebuild time, not a correctness problem, and Docker layer caching absorbs most of it. This was raised and accepted.

- **D-02:** **PostgreSQL stays in its own container** — the official `postgres:17` image with a named volume (PKG-04). Bundling the database into the app image was considered and **rejected**: it means a hand-rolled multi-process image under a supervisor, and it ties the ledger's storage lifecycle to the app image. PROJECT.md's core value is "accurate and never lost", backups are explicitly deferred this milestone, and the homelab disk is the only copy — so the boring, well-trodden path wins here. "One container" means one *app* container, not one container total.

- **D-03:** **Compose has exactly two services** — `db` and the app. Only the app service publishes a port to the host. This makes Phase 3's ACC-03 ("only the web entry point publishes a port") simpler rather than harder: there is only one candidate.

- **D-04:** **Cache headers (PKG-06) are C# `StaticFileOptions.OnPrepareResponse`**, not proxy config — a direct consequence of D-01. Roughly: long immutable caching for Vite's content-hashed assets, no-cache for `index.html` and the service worker, so an installed PWA actually picks up a redeploy instead of pinning a stale bundle.

### API Base URL (ORIG-01, ORIG-02)

- **D-05:** **`VITE_API_URL` is deleted entirely.** `src/Okozukai.Frontend/src/api/client.ts:4` currently reads `baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5005'`; the env var and the localhost fallback both go away and `baseURL` becomes empty/relative. Keeping it as a dev-only override was offered and rejected — success criterion 1 ("the built bundle contains no absolute API URL") should hold *by construction*, with no code path capable of baking a URL in, rather than depending on a variable being unset at image-build time. — **Reversibility:** reversible — one line in `client.ts`, though the consequences below spread wider.

- **D-06:** **Consequence of D-05 — `aspire run` needs a Vite dev-server proxy.** With no absolute base URL, the dev server and API are no longer bridgeable by env var, so `vite.config.ts` gains a `server.proxy` entry forwarding `/api` to the API. The `VITE_API_URL` / `TAILNET_API_PORT` injection in `src/Okozukai.AppHost/Program.cs` comes out. This narrows D-15 from Phase 1 ("AppHost stays dev-only and untouched") — the AppHost is still dev-only, but it is no longer untouched. Not discussed in detail; the planner works out the proxy shape. **Check the 14 Playwright E2E tests** — they may carry base-URL assumptions.

- **D-07:** **CORS is removed outright** — delete the `AddCors`/`AddDefaultPolicy` block at `src/Okozukai.Api/Program.cs:13-21` and the `app.UseCors()` call at line 58. Nothing in the deployed topology is cross-origin, and with D-06's dev proxy nothing in development is either.

### Health Endpoint Exposure

- **D-08:** **`/health` and `/alive` become reachable from the Tailnet, and that is accepted.** Phase 1's D-11 kept them safe by relying on "the API port is never published to the host"; with one app container they sit on the same published port as the app. **Do not add `RequireHost`, auth, or a management port to compensate** — the user weighed this explicitly and judged it not worth plumbing on a single-user Tailnet box with no real-money data. This supersedes the Phase 1 threat T-01-02 mitigation-by-topology; record it as an accepted risk, not an open threat.

- **D-09:** The Phase 1 health contract (D-12) is unchanged and is what compose consumes: `/health` = ready (includes the PostgreSQL check), `/alive` = live (process-only `self` check). Use these for the Dockerfile `HEALTHCHECK` and `depends_on: condition: service_healthy`. **Do not add health-check packages or DI wiring** — Aspire's `AddNpgsqlDbContext` already registers the PostgreSQL check (Phase 1 D-10).

### Claude's Discretion

The user chose to lock the entry-point shape and leave the rest to the planner, on the grounds that these are all reversible if wrong:

- Dockerfile stage layout — node stage building `dist/`, dotnet stage publishing the API, final image copying both. Base images, non-root user, and whether to use `dotnet publish /t:PublishContainer` instead of a hand-written Dockerfile.
- Where the built SPA lands in the image (`wwwroot` is the obvious choice) and exact middleware ordering around `UseStaticFiles` / `MapFallbackToFile`.
- Exact cache-header values and durations for D-04.
- Compose file details: named volume name, `env_file` wiring, restart policy, `depends_on` conditions.
- The `vite.config.ts` dev-proxy shape (D-06).
- Whether the app container gets its own `HEALTHCHECK`, and behaviour when the API is down (maintenance page etc.) — explicitly called low-stakes.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level planning
- `.planning/PROJECT.md` — Constraints pin the tech stack, the single-Linux-box Docker target, and Tailnet-only access as the security model. Key Decisions records the `AddViteApp`/`PublishAsStaticWebsite` choice, which D-01 now supersedes for the deployment path (`AddViteApp` remains the dev-time orchestration mechanism). Core Value — "the recorded ledger is accurate and never lost" — is the reasoning behind D-02.
- `.planning/REQUIREMENTS.md` — ORIG-01/02/03 (lines 28-30) and PKG-01…06 (lines 36-41), the nine requirements this phase closes. §Out of Scope rules out CI/CD, backups, authentication, and horizontal scaling for this milestone.
- `.planning/ROADMAP.md` §"Phase 2: Single-Origin Packaging" — the five success criteria this phase is verified against.
- `.planning/STATE.md` §Blockers/Concerns — three items explicitly tagged `[Phase 2]`: the `CREATEDB` requirement for the `3D000` fallback, the health-check contract, and deploy-time verification deferred from Phase 1 UAT (interrupted mid-migration startup; Npgsql quoting for a password containing `;` or `=`).

### Prior phase decisions (binding)
- `.planning/phases/01-production-readiness/01-CONTEXT.md` — D-01/D-02 (unconditional migrations, crash on failure), D-03 (`3D000` auto-create fallback active in Production), D-07 (plain HTTP, no TLS terminator, no `UseForwardedHeaders`), D-08 (`ASPNETCORE_URLS` from environment), D-10 (Aspire registers the PostgreSQL health check — do not add one), D-11/D-12 (health contract), D-13 (`ConnectionStrings__okozukai` env var), D-16 (`.env.example` committed, `.env` gitignored — this is PKG-05's mechanism). Its §Deferred Ideas carries the forward constraint that the homelab box is itself a self-hosted GitHub Actions runner, so **local build-and-run must stay viable and no container registry hop is required**.
- `.planning/phases/01-production-readiness/01-SECURITY.md` — accepted risk R-01 (DB password visible to `docker inspect`) and threat T-01-02, which D-08 above revisits.

### Codebase maps
- `.planning/codebase/ARCHITECTURE.md` — layering rules and entry points.
- `.planning/codebase/STACK.md` §Configuration — environment-variable inventory, including the `VITE_API_URL` entry that D-05 removes.

### Files this phase modifies
- `src/Okozukai.Api/Program.cs` — lines 13-21 and 58 (CORS, to delete), line 62 (`MapControllers`, static-file and fallback middleware goes after).
- `src/Okozukai.Frontend/src/api/client.ts` — line 4 (`baseURL`).
- `src/Okozukai.Frontend/vite.config.ts` — `server` block gains a `/api` proxy.
- `src/Okozukai.AppHost/Program.cs` — `VITE_API_URL` / `TAILNET_API_PORT` injection comes out.
- New at repo root: `compose.yaml`, `Dockerfile`, `.dockerignore`.

**No external ADRs or specs exist for this project** — the decisions above and Phase 1's CONTEXT.md are the authoritative record.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Frontend service files already send fully-qualified relative paths — `journalService.ts:6` (`'/api/journals'`), `transactionService.ts:19` (`'/api/transactions'`). Nothing but `client.ts:4`'s `baseURL` stands between the current code and relative calls; **no service file needs editing for ORIG-01**.
- `vite-plugin-pwa` 1.2.0 is already configured in `vite.config.ts` with `registerType: 'autoUpdate'`. PKG-06 is about serving its output with the right headers, not about adding PWA support.
- `MigrationExtensions.ApplyDatabaseMigrations` and the Aspire-registered PostgreSQL health check are production-ready from Phase 1 — the container work consumes them, it does not modify them.

### Established Patterns
- Clean architecture with one-directional dependencies; only `Okozukai.Infrastructure` references EF Core. Static-file serving is an Api-layer concern and touches nothing below it.
- Configuration flows through standard `IConfiguration` providers; `ConnectionStrings__okozukai` as an env var needs no code to read (Phase 1 D-13).
- `src/Okozukai.Api/Properties/launchSettings.json` is dev-only and ignored in containers.

### Integration Points
- `src/Okozukai.Api/Program.cs` is where every backend change in this phase lands.
- `Program` is `public partial` (line 66) for `WebApplicationFactory`; the 22 integration tests boot through it, so adding static-file middleware must not assume `wwwroot` exists — **it will not exist when the tests run from the source tree**.
- `src/Okozukai.AppHost/Program.cs` is the dev-time seam; `AddViteApp` registers the frontend's http endpoint and `PORT` env var itself (noted in an existing comment there).

### Known Constraints
- No Dockerfile, compose file, or `.dockerignore` exists anywhere in the repo yet — this phase creates all of them from scratch.
- `src/Okozukai.Web/` is an empty leftover project containing only `obj/` build artifacts. It is not part of this phase, but do not mistake it for the web entry point.
- The working tree has uncommitted changes in `.planning/` and untracked `.gsd/` and `.planning/research/`. Planning should account for a non-clean starting tree.

</code_context>

<specifics>
## Specific Ideas

- The user's stated goal for this phase is **simplicity**, arrived at mid-discussion: after picking a separate nginx container, they asked whether the whole app could be collapsed into one container. It can, and doing so is less work — that reversal (D-01) is the defining decision of this phase. Downstream agents should treat "fewer moving parts" as the tie-breaker on discretionary calls, not "more defensive".
- The user pushed back on security-flavoured framing, correctly: this is a journal app on a Tailnet-only homelab box with no real money at stake. The honest answer given, and the one that should guide planning: exposure decisions here are near-zero consequence, while **data durability is the thing that actually matters** — PKG-04's named volume is the highest-stakes item in the phase, because the homelab disk is the only copy of the ledger and backups are deferred. Do not re-litigate the health-endpoint exposure (D-08).

</specifics>

<deferred>
## Deferred Ideas

- **Everything in one container, Postgres included** — raised and rejected in favour of D-02. Not a future phase item; recorded so it is not re-proposed.
- **`RequireHost` / auth / management port for health endpoints** — declined under D-08. Would only become relevant if the app ever leaves the Tailnet, which PROJECT.md already flags as the trigger for revisiting the whole authentication question.
- **Volume/data hazards not discussed in detail** — the user chose to leave these to the planner, but STATE.md flags them and they touch the phase's highest-stakes requirement: whether `POSTGRES_DB` handles provisioning on first container boot (making the `3D000` fallback and its `CREATEDB` assumption moot), and the `DevSeedData.SeedDevelopmentData` wipe risk if the stack ever starts with `ASPNETCORE_ENVIRONMENT` unset. Both are already tracked in `.planning/STATE.md` §Blockers/Concerns and PROJECT.md §Active.
- **Caching & PWA update behaviour beyond D-04** — whether `registerType` stays `autoUpdate` (silent reload on redeploy) or becomes a prompt was offered as a discussion area and not selected. Planner's discretion; `autoUpdate` is the current setting and the simpler default.
- **GitHub Actions deployment workflow** — carried forward from Phase 1. CI/CD is Out of Scope for milestone 1, but the forward constraint stands: the homelab box is a self-hosted runner, so a future workflow builds and runs in place with no registry hop.

</deferred>

---

*Phase: 2-Single-Origin Packaging*
*Context gathered: 2026-08-21*
