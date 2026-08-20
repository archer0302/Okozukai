# Phase 1: Production Readiness - Context

**Gathered:** 2026-08-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Okozukai's API behaves correctly when started with `ASPNETCORE_ENVIRONMENT=Production` — the hosting mode it will run in on the homelab — instead of only working under Development. Covers PROD-01 through PROD-05: automatic migrations, seed-data suppression, proxy/TLS behaviour, health endpoints, and environment-only configuration.

**In scope:** changes to `src/Okozukai.Api/Program.cs`, `src/Okozukai.ServiceDefaults/Extensions.cs`, `src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs`, plus a committed `.env.example` and a `.gitignore` entry.

**Out of scope (later phases):** container images and Dockerfiles (PKG-01/02, Phase 2), compose stack and `depends_on` wiring (PKG-03, Phase 2), CORS removal and single-origin routing (ORIG-01/02/03, Phase 2), Tailnet exposure and port publishing (ACC-01/02/03, Phase 3), query indexes (PERF-01/02, Phase 4).

</domain>

<decisions>
## Implementation Decisions

### Migration Strategy (PROD-01, PROD-02)

- **D-01:** Migrations apply on API startup **unconditionally** — move `app.Services.ApplyDatabaseMigrations()` out of the `IsDevelopment()` block at `src/Okozukai.Api/Program.cs:38-44`. Rejected alternatives: a separate one-shot migrator container, and an opt-in `RUN_MIGRATIONS` flag. Rationale: single API instance is a locked constraint (horizontal scaling is explicitly Out of Scope in REQUIREMENTS.md), and the existing 10× retry loop with 3s backoff already covers a database that isn't accepting connections yet. — **Reversibility:** reversible — the call site is one line in `Program.cs`; splitting migrations into a separate entrypoint later touches no application code.

- **D-02:** Migration failure **crashes the process**. `ApplyDatabaseMigrations` already rethrows once retries are exhausted (`MigrationExtensions.cs:56`); keep that. The container exits non-zero and Docker's restart policy retries the whole boot. Never serve a ledger against a half-migrated schema.

- **D-03:** Keep the `3D000` auto-create-database fallback (`MigrationExtensions.cs:39-53`) active in Production as a safety net. The Postgres container's `POSTGRES_DB` will normally create `okozukai` on first boot so this path should not fire, but if it does a fresh deployment comes up instead of crash-looping. Assumes the app's role has `CREATEDB`, which is acceptable on a single-tenant box. Explicitly **not** gated to Development — divergent boot paths between environments are the exact problem this phase exists to remove.

- **D-04:** `SeedDevelopmentData()` **stays inside** the `IsDevelopment()` guard. PROD-02 is satisfied by D-01 alone: once the migration call moves out, the guard's only remaining occupant is the seed call. No defensive throw, no compile-time exclusion.

- **D-05:** Verification for this phase is a **manual Production run against an empty scratch database** — create an empty local database, start the API with `ASPNETCORE_ENVIRONMENT=Production` and env-var config, confirm all six migrations apply and no seed rows exist. No Testcontainers or Production-mode `WebApplicationFactory` work: the existing 22 integration tests do not spin up a real PostgreSQL, and introducing that infrastructure is disproportionate inside a phase that is otherwise configuration changes.

### Proxy & TLS Handling (PROD-03)

- **D-06:** **Remove `UseHttpsRedirection()` entirely** (`src/Okozukai.Api/Program.cs:50-53`). The container listens on plain HTTP on an internal network and TLS is not terminated anywhere in the topology, so redirecting inside the container is meaningless and is the direct cause of the redirect loop.

- **D-07:** Homelab topology is **plain HTTP over Tailscale, no TLS terminator**. Tailscale encrypts the wire; network isolation is the security model for this milestone (PROJECT.md Constraints). Consequently **no `UseForwardedHeaders`** is added — no proxy will send `X-Forwarded-Proto`. Success criterion 3 holds because there is no redirect to loop on. — **Reversibility:** costly — this choice propagates into Phase 2's entry point (whether a TLS-terminating proxy container exists at all) and Phase 3's Tailnet wiring; introducing TLS later means revisiting both plus adding forwarded-header trust configuration.

- **D-08:** Kestrel binds **HTTP only, via `ASPNETCORE_URLS` supplied from the environment** (e.g. `http://+:8080`). No HTTPS endpoint, so no dev-cert or PFX plumbing in the container. `src/Okozukai.Api/Properties/launchSettings.json` is dev-only and is ignored in containers — leave it as-is, including its `https` profile.

### Health Check Scope (PROD-04)

- **D-09:** **Remove the `IsDevelopment()` gate** in `src/Okozukai.ServiceDefaults/Extensions.cs:113-123` so `/health` and `/alive` map in every environment. This is the whole of the code change for PROD-04.

- **D-10:** **No new health-check code is required.** `builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai")` at `src/Okozukai.Api/Program.cs:22` comes from `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.1.1, whose `AddNpgsqlDbContext` already registers a PostgreSQL health check by default (`NpgsqlEntityFrameworkCorePostgreSQLSettings.DisableHealthChecks` defaults to `false`). That check is untagged, so it runs on `/health` (no predicate → all checks) but not on `/alive` (predicate: `Tags.Contains("live")` → only the `self` check). The readiness/liveness split falls out for free. **Do not add a package reference or DI wiring for a database health check.**

- **D-11:** Health endpoints stay on the **ordinary API port**. PROD-04's "internal port" is satisfied by topology, not by app code: Phase 3's ACC-03 means the API port is never published to the host, so the endpoints exist only on the internal container network. Rejected: a dedicated management port with `RequireHost` — defence in depth against a threat the Tailnet model already covers.

- **D-12:** Phase 1 **exposes only**. The Dockerfile `HEALTHCHECK` and any compose `depends_on: condition: service_healthy` belong to Phase 2, where the container work lands. **Contract for Phase 2:** `/health` = ready (includes the PostgreSQL check), `/alive` = live (process only, `self` check).

### Config & Secrets Shape (PROD-05)

- **D-13:** The connection string reaches the API as the **`ConnectionStrings__okozukai` environment variable**. ASP.NET Core's environment-variable provider maps the double underscore to the config section, landing it exactly where `AddNpgsqlDbContext("okozukai")` already looks — no code change is needed to read it. Rejected: discrete `POSTGRES_*` variables composed at startup (new startup code, diverges from the shape Aspire expects) and a Docker-secret `_FILE` pattern (custom config loading for a single-user box on a private tailnet). Accepted trade-off: the password is visible to `docker inspect`.

- **D-14:** **Fail fast with an explicit message** when the connection string is missing or empty at startup — validate before EF/Npgsql is touched, and name the missing key (`ConnectionStrings__okozukai`) in the error. Without this, the failure surfaces from inside the migration retry loop and produces ten rounds of opaque Npgsql noise before the real cause appears.

- **D-15:** The **Aspire AppHost stays dev-only and untouched** in this phase. `src/Okozukai.AppHost/Program.cs:13` keeps its hardcoded `ASPNETCORE_ENVIRONMENT=Development`, and the AppHost keeps sourcing the connection string from `dotnet user-secrets`. Compose is the production runner; `aspire run` remains the local development experience. PROD-05 is about the API, not the orchestrator.

- **D-16:** A **committed `.env.example`** documents every required variable (`ConnectionStrings__okozukai`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`); the real **`.env` is gitignored**. This gives Phase 2's compose file something to consume via `env_file` and satisfies PKG-05's "not committed to the repository". Note `.gitignore` currently has no `.env` entry — one must be added.

### Claude's Discretion

- Exact port number in `ASPNETCORE_URLS` (D-08) — 8080 is the .NET container image default and the obvious choice, but nothing depends on it until Phase 2 fixes the deployment contract.
- Where the fail-fast connection-string guard lives (D-14) — inline in `Program.cs` before `AddNpgsqlDbContext`, or an extension method alongside `AddInfrastructure`. Follow existing conventions.
- Whether `appsettings.Production.json` is created at all, and Production log levels. Not discussed; `appsettings.json` already sets sane defaults (`Default: Information`, `Microsoft.AspNetCore: Warning`).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level planning
- `.planning/PROJECT.md` — Constraints section pins the tech stack, the single-Linux-box Docker target, and Tailnet-only access as the security model. Key Decisions table records the `AddViteApp`/`PublishAsStaticWebsite` choice that shapes Phase 2.
- `.planning/REQUIREMENTS.md` §Production Readiness — PROD-01 … PROD-05, the requirements this phase closes. §Out of Scope rules out CI/CD, backups, authentication, and horizontal scaling for this milestone.
- `.planning/ROADMAP.md` §"Phase 1: Production Readiness" — the five success criteria this phase is verified against.

### Codebase maps
- `.planning/codebase/ARCHITECTURE.md` — layering rules (only Infrastructure touches EF Core), entry points, and the note that migrations are dev-startup-only today.
- `.planning/codebase/STACK.md` §Configuration — the current environment-variable inventory and the user-secrets arrangement this phase replaces.
- `.planning/codebase/INTEGRATIONS.md` §Data Storage — connection configuration and the `ConnectionStrings:okozukai` key name.

### Files this phase modifies
- `src/Okozukai.Api/Program.cs` — lines 38-44 (migration/seed gate), 50-53 (HTTPS redirection), 22 (`AddNpgsqlDbContext`).
- `src/Okozukai.ServiceDefaults/Extensions.cs` — lines 109-126 (`MapDefaultEndpoints`, the `IsDevelopment()` gate on health endpoints).
- `src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs` — the retry loop and `3D000` auto-create fallback that Production now depends on.
- `.gitignore` — needs a `.env` entry (D-16).

**No external ADRs or specs exist for this project** — decisions above are the authoritative record.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MigrationExtensions.ApplyDatabaseMigrations` (`src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs:11`) — already production-grade: 10 retries at 3s intervals, non-relational-provider skip, `3D000` auto-create fallback, structured logging, rethrow on exhaustion. It is never *called* in Production; the mechanism itself needs no change.
- `Extensions.MapDefaultEndpoints` (`src/Okozukai.ServiceDefaults/Extensions.cs:109`) — `/health` and `/alive` are already written with the correct readiness/liveness tag predicate. Only the environment gate is wrong.
- Aspire's built-in PostgreSQL health check, registered implicitly by `AddNpgsqlDbContext` — see D-10.
- OpenTelemetry trace filter (`Extensions.cs:66-70`) already excludes `/health` and `/alive` from tracing, so mapping them outside Development will not pollute traces.

### Established Patterns
- Clean architecture with one-directional dependencies; only `Okozukai.Infrastructure` references EF Core. Migration and seed helpers live in `Infrastructure/Persistence` and are invoked from `Program.cs` via `IServiceProvider` extension methods — follow that shape for any new startup code.
- Configuration is read through the standard `IConfiguration` providers; there is no custom config layer to extend.
- `appsettings.json` carries only logging and `AllowedHosts: "*"`; there is no `appsettings.Production.json`.

### Integration Points
- `src/Okozukai.Api/Program.cs` is the single entry point where every Phase 1 change lands except the health-endpoint gate.
- `builder.AddServiceDefaults()` (`Program.cs:9`) wires OpenTelemetry, health checks, and service discovery — the seam into `ServiceDefaults`.
- The API's `Program` class is `public partial` (`Program.cs:61`) for `WebApplicationFactory`; the 22 existing integration tests boot through it, so startup changes must not assume a Production-only path exists.

### Known Constraints
- The working tree has uncommitted changes to `src/Okozukai.AppHost/Program.cs`, `.gitignore`, `README.md`, and `src/Okozukai.Frontend/e2e/dashboard.spec.ts` staged from prior work. Planning should account for a non-clean starting tree.

</code_context>

<specifics>
## Specific Ideas

- The user pushed back on adding a PostgreSQL readiness probe, asking what value it has with no dashboard watching it. The answer that resolved it: the probe requires no work because Aspire already registers it (D-10), and Docker does **not** restart containers on failing health checks — the real consumers are compose startup ordering and `docker ps` at a glance. Downstream agents should not re-argue this, and should not add health-check packages.
- The user asked that the configuration shape not preclude a future GitHub Actions deployment. It does not: `ConnectionStrings__okozukai` as an env var is exactly what a deploy workflow injects, and `.env.example` is the contract naming the keys a workflow must write.

</specifics>

<deferred>
## Deferred Ideas

- **GitHub Actions deployment workflow** — CI/CD is explicitly Out of Scope for milestone 1 (`.planning/REQUIREMENTS.md` §Out of Scope: *"Deploy is manual for now; automating it before it works once would be premature"*). **Forward constraint for Phase 2:** the homelab box is itself a self-hosted GitHub Actions runner, so a future workflow builds images in place and runs `docker compose up` locally — no container registry hop (GHCR is optional rather than required for PKG-01) and no `tailscale/github-action` runner plumbing. Phase 2's packaging decisions should keep local-build-and-run viable.
- **Narrowing `AllowedHosts` from `"*"`** — raised as a possible follow-up during the proxy discussion, not pursued. Low value while the app is Tailnet-only.
- **`appsettings.Production.json` and Production log levels** — surfaced but not discussed. Left to Claude's discretion (see above) or a later pass.

</deferred>

---

*Phase: 1-Production Readiness*
*Context gathered: 2026-08-20*
