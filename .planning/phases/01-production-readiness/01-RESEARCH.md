# Phase 1: Production Readiness - Research

**Researched:** 2026-08-20
**Domain:** ASP.NET Core 10 production startup behavior (migrations, health checks, proxy/TLS, environment-variable configuration)
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Migration Strategy (PROD-01, PROD-02)**
- **D-01:** Migrations apply on API startup **unconditionally** — move `app.Services.ApplyDatabaseMigrations()` out of the `IsDevelopment()` block at `src/Okozukai.Api/Program.cs:38-44`. Rejected alternatives: a separate one-shot migrator container, and an opt-in `RUN_MIGRATIONS` flag. Rationale: single API instance is a locked constraint (horizontal scaling is explicitly Out of Scope), and the existing 10× retry loop with 3s backoff already covers a database that isn't accepting connections yet. Reversibility: reversible — one line in `Program.cs`.
- **D-02:** Migration failure **crashes the process**. `ApplyDatabaseMigrations` already rethrows once retries are exhausted (`MigrationExtensions.cs:56`); keep that. The container exits non-zero and Docker's restart policy retries the whole boot. Never serve a ledger against a half-migrated schema.
- **D-03:** Keep the `3D000` auto-create-database fallback (`MigrationExtensions.cs:39-53`) active in Production as a safety net. `POSTGRES_DB` will normally create `okozukai` on first boot so this path should not fire, but if it does a fresh deployment comes up instead of crash-looping. Assumes the app's role has `CREATEDB`, acceptable on a single-tenant box. Explicitly **not** gated to Development.
- **D-04:** `SeedDevelopmentData()` **stays inside** the `IsDevelopment()` guard. PROD-02 is satisfied by D-01 alone: once the migration call moves out, the guard's only remaining occupant is the seed call. No defensive throw, no compile-time exclusion.
- **D-05:** Verification for this phase is a **manual Production run against an empty scratch database** — create an empty local database, start the API with `ASPNETCORE_ENVIRONMENT=Production` and env-var config, confirm all six migrations apply and no seed rows exist. No Testcontainers or Production-mode `WebApplicationFactory` work: the existing 22 integration tests do not spin up a real PostgreSQL, and introducing that infrastructure is disproportionate inside a phase that is otherwise configuration changes.

**Proxy & TLS Handling (PROD-03)**
- **D-06:** **Remove `UseHttpsRedirection()` entirely** (`src/Okozukai.Api/Program.cs:50-53`). The container listens on plain HTTP on an internal network and TLS is not terminated anywhere in the topology, so redirecting inside the container is meaningless and is the direct cause of the redirect loop.
- **D-07:** Homelab topology is **plain HTTP over Tailscale, no TLS terminator**. Tailscale encrypts the wire; network isolation is the security model for this milestone. Consequently **no `UseForwardedHeaders`** is added — no proxy will send `X-Forwarded-Proto`. Success criterion 3 holds because there is no redirect to loop on. Reversibility: costly — propagates into Phase 2's entry point and Phase 3's Tailnet wiring; introducing TLS later means revisiting both plus adding forwarded-header trust configuration.
- **D-08:** Kestrel binds **HTTP only, via `ASPNETCORE_URLS` supplied from the environment** (e.g. `http://+:8080`). No HTTPS endpoint, so no dev-cert or PFX plumbing in the container. `src/Okozukai.Api/Properties/launchSettings.json` is dev-only and is ignored in containers — leave it as-is, including its `https` profile.

**Health Check Scope (PROD-04)**
- **D-09:** **Remove the `IsDevelopment()` gate** in `src/Okozukai.ServiceDefaults/Extensions.cs:113-123` so `/health` and `/alive` map in every environment. This is the whole of the code change for PROD-04.
- **D-10:** **No new health-check code is required.** `builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai")` at `src/Okozukai.Api/Program.cs:22` comes from `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.1.1, whose `AddNpgsqlDbContext` already registers a PostgreSQL health check by default (`DisableHealthChecks` defaults to `false`). That check is untagged, so it runs on `/health` (no predicate → all checks) but not on `/alive` (predicate: `Tags.Contains("live")` → only the `self` check). The readiness/liveness split falls out for free. **Do not add a package reference or DI wiring for a database health check.**
- **D-11:** Health endpoints stay on the **ordinary API port**. PROD-04's "internal port" is satisfied by topology, not by app code: Phase 3's ACC-03 means the API port is never published to the host, so the endpoints exist only on the internal container network. Rejected: a dedicated management port with `RequireHost` — defence in depth against a threat the Tailnet model already covers.
- **D-12:** Phase 1 **exposes only**. The Dockerfile `HEALTHCHECK` and any compose `depends_on: condition: service_healthy` belong to Phase 2. **Contract for Phase 2:** `/health` = ready (includes the PostgreSQL check), `/alive` = live (process only, `self` check).

**Config & Secrets Shape (PROD-05)**
- **D-13:** The connection string reaches the API as the **`ConnectionStrings__okozukai` environment variable**. ASP.NET Core's environment-variable provider maps the double underscore to the config section, landing it exactly where `AddNpgsqlDbContext("okozukai")` already looks — no code change is needed to read it. Rejected: discrete `POSTGRES_*` variables composed at startup, and a Docker-secret `_FILE` pattern. Accepted trade-off: the password is visible to `docker inspect`.
- **D-14:** **Fail fast with an explicit message** when the connection string is missing or empty at startup — validate before EF/Npgsql is touched, and name the missing key (`ConnectionStrings__okozukai`) in the error. Without this, the failure surfaces from inside the migration retry loop and produces ten rounds of opaque Npgsql noise before the real cause appears.
- **D-15:** The **Aspire AppHost stays dev-only and untouched** in this phase. `src/Okozukai.AppHost/Program.cs:13` keeps its hardcoded `ASPNETCORE_ENVIRONMENT=Development`, and the AppHost keeps sourcing the connection string from `dotnet user-secrets`. Compose is the production runner; `aspire run` remains the local development experience. PROD-05 is about the API, not the orchestrator.
- **D-16:** A **committed `.env.example`** documents every required variable (`ConnectionStrings__okozukai`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`); the real **`.env` is gitignored**. This gives Phase 2's compose file something to consume via `env_file` and satisfies PKG-05's "not committed to the repository". CONTEXT.md's own text notes: *"`.gitignore` currently has no `.env` entry — one must be added."* **This claim is stale — see Assumptions Log A2 and Open Questions.**

### Claude's Discretion
- Exact port number in `ASPNETCORE_URLS` (D-08) — 8080 is the .NET container image default and the obvious choice, but nothing depends on it until Phase 2 fixes the deployment contract.
- Where the fail-fast connection-string guard lives (D-14) — inline in `Program.cs` before `AddNpgsqlDbContext`, or an extension method alongside `AddInfrastructure`. Follow existing conventions.
- Whether `appsettings.Production.json` is created at all, and Production log levels. Not discussed; `appsettings.json` already sets sane defaults (`Default: Information`, `Microsoft.AspNetCore: Warning`).

### Deferred Ideas (OUT OF SCOPE)
- **GitHub Actions deployment workflow** — CI/CD is explicitly Out of Scope for milestone 1. Forward constraint for Phase 2: the homelab box is itself a self-hosted GitHub Actions runner, so a future workflow builds images in place and runs `docker compose up` locally — no container registry hop and no `tailscale/github-action` runner plumbing.
- **Narrowing `AllowedHosts` from `"*"`** — raised, not pursued. Low value while the app is Tailnet-only.
- **`appsettings.Production.json` and Production log levels** — surfaced but not discussed. Left to Claude's discretion or a later pass.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| PROD-01 | DB schema created/migrated automatically on Production startup | Verified `ApplyDatabaseMigrations` (10× retry, `3D000` auto-create fallback) is already production-grade at `src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs:11-60`; only its call site needs to move (D-01). See Code Examples, Architecture Patterns. |
| PROD-02 | Dev seed data never inserted outside Development | Verified `SeedDevelopmentData()` call sits alongside the migration call in the same `IsDevelopment()` block (`Program.cs:38-44`); moving only the migration call out satisfies this with zero new logic (D-04). See Code Examples. |
| PROD-03 | Honour `X-Forwarded-Proto`, no redirect loop | Verified the topology (D-07) has no TLS terminator, so the standard `UseForwardedHeaders` fix does not apply here — the correct fix is removing `UseHttpsRedirection()` (D-06). See Common Pitfalls, Sources. |
| PROD-04 | Health endpoints respond outside Development, internal port | Verified `MapDefaultEndpoints`'s `IsDevelopment()` gate is the only blocker (`Extensions.cs:113-123`) and that `AddNpgsqlDbContext` already registers an untagged DB health check reachable only via `/health` (D-10). See Code Examples, Standard Stack. |
| PROD-05 | All config incl. connection string from env vars, no user-secrets | Verified ASP.NET Core's built-in double-underscore env-var provider needs no code change to read `ConnectionStrings__okozukai` (D-13); only the fail-fast guard (D-14) is new code. See Code Examples, Common Pitfalls. |
</phase_requirements>

## Summary

This phase is almost entirely a *removal* and *relocation* exercise, not new engineering. Every mechanism PROD-01 through PROD-05 needs already exists in the codebase in production-grade form — the retry/backoff migration helper, the `3D000` auto-create fallback, the readiness/liveness health-check split, and the environment-variable configuration provider are all already correct. The five requirements are closed by moving two lines out of an `IsDevelopment()` guard (D-01), deleting one gate around health-check mapping (D-09), deleting one middleware call (D-06), and adding one fail-fast guard plus one documentation file (D-14, D-16). Codebase inspection this session (line-numbers below) confirms every file/line reference in CONTEXT.md's decisions is currently accurate, with one exception: **`.gitignore` already contains a `.env` entry** (added in commit `880a8eb`, after CONTEXT.md's context-gathering session) — D-16's instruction to add one is a no-op that the planner should not schedule as a task.

The riskiest engineering judgment in this phase is D-07/D-06: removing `UseHttpsRedirection()` entirely rather than adding the textbook `UseForwardedHeaders` fix. Standard ASP.NET Core guidance for the classic proxy redirect loop is to trust `X-Forwarded-Proto` via `UseForwardedHeaders` placed before `UseHttpsRedirection`. This phase instead removes the redirect middleware outright, which is the *correct* choice given the locked topology decision (plain HTTP over Tailscale, no TLS terminator anywhere) — but it is a topology-contingent decision, not a general pattern, and the research below documents why the standard fix does not apply here.

**Primary recommendation:** Implement the four `Program.cs`/`Extensions.cs` edits and the connection-string guard exactly as CONTEXT.md specifies; treat D-16's `.gitignore` claim as already satisfied and scope that task down to "create `.env.example`" only; verify with the manual Production-mode run against an empty scratch database (D-05) using the locally available `dotnet 10.0.100` / `PostgreSQL 17.10` / `docker 29.6.1` toolchain, all confirmed present in this environment.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Automatic DB schema migration on startup | API / Backend | Database / Storage | `EF Core Migrate()` executes inside the API process at boot (`Infrastructure/Persistence/MigrationExtensions.cs`) but directly mutates the database's physical schema |
| Dev-seed-data suppression | API / Backend | — | Pure conditional logic in `Program.cs`; no other tier involved |
| Proxy/TLS redirect handling | API / Backend | — | Middleware pipeline ordering decision inside the ASP.NET Core host; this milestone's topology has no separate reverse-proxy tier |
| Health-check endpoints | API / Backend | Database / Storage | `/health` aggregates a check that reaches into Postgres (`CanConnectAsync`); `/alive` is process-only and stays entirely in the API tier |
| Environment-variable configuration | API / Backend | — | `IConfiguration` providers resolve at host-builder time; no secrets manager or CDN tier exists in this milestone |

## Package Legitimacy Audit

**Not applicable this phase.** No new external packages are installed. Every mechanism this phase relies on (`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.1.1, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0, the OpenTelemetry/ServiceDiscovery/Resilience packages in `Okozukai.ServiceDefaults`) is an existing dependency, confirmed present via direct `.csproj` reads this session — see Standard Stack. D-10 explicitly directs: *"Do not add a package reference or DI wiring for a database health check."* If a future plan step considers any new package, run the Package Legitimacy Gate at that time.

## Standard Stack

### Core (existing — no new installs)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | 13.1.1 [VERIFIED: src/Okozukai.Api/Okozukai.Api.csproj:10] | `AddNpgsqlDbContext<T>` DbContext registration, connection resilience, and an implicit `DbContextHealthCheck` | Already wired at `Program.cs:22`; Aspire's blessed EF Core integration is the source of the health check this phase relies on for PROD-04 (D-10) |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.0 [VERIFIED: src/Okozukai.Infrastructure/Okozukai.Infrastructure.csproj:14] | Underlying EF Core PostgreSQL provider | Wrapped by the Aspire package above; `IsRelational()` / `Database.Migrate()` calls in `MigrationExtensions.cs` depend on this provider |
| `Microsoft.Extensions.ServiceDiscovery`, `Microsoft.Extensions.Http.Resilience` | 10.1.0 [VERIFIED: src/Okozukai.ServiceDefaults/Okozukai.ServiceDefaults.csproj:12-13] | Service discovery + HTTP resilience defaults | Part of `AddServiceDefaults()`, unmodified by this phase except the health-endpoint gate |
| `OpenTelemetry.*` (Exporter.OpenTelemetryProtocol, Extensions.Hosting, Instrumentation.AspNetCore/Http/Runtime) | 1.14.0 [VERIFIED: src/Okozukai.ServiceDefaults/Okozukai.ServiceDefaults.csproj:14-18] | Tracing/metrics; already excludes `/health` and `/alive` from traces | Confirms mapping health endpoints outside Development (D-09) will not pollute traces — filter is at `Extensions.cs:66-70` |

**No installation required.** This phase edits call sites and middleware registration only.

**Version verification performed this session:**
```bash
# Confirmed by reading .csproj files directly, not via registry lookup — these are
# existing pinned versions already restored and building in this repo, not new picks.
grep -n "PackageReference" src/Okozukai.Api/Okozukai.Api.csproj
grep -n "PackageReference" src/Okozukai.Infrastructure/Okozukai.Infrastructure.csproj
grep -n "PackageReference" src/Okozukai.ServiceDefaults/Okozukai.ServiceDefaults.csproj
```

### Alternatives Considered (already rejected in CONTEXT.md — do not re-litigate)
| Instead of | Could Use | Tradeoff (why rejected) |
|------------|-----------|--------------------------|
| Unconditional migration on API startup (D-01) | Separate one-shot migrator container | Extra deployable, extra compose service, unnecessary when single-instance is a locked constraint |
| Unconditional migration (D-01) | Opt-in `RUN_MIGRATIONS` env flag | Reintroduces a divergent boot path — exactly what this phase exists to remove |
| `UseHttpsRedirection()` removal (D-06) | `UseForwardedHeaders` + keep redirect | Standard fix for a proxy that *does* terminate TLS; this topology (D-07) has no such proxy, so the standard fix has nothing to configure and adds a false sense of protection |
| `ConnectionStrings__okozukai` env var (D-13) | Discrete `POSTGRES_*` vars composed at startup | New startup code to compose a connection string; diverges from the shape Aspire's `AddNpgsqlDbContext` already expects |
| `ConnectionStrings__okozukai` env var (D-13) | Docker secret `_FILE` pattern | Custom config-loading code for a single-user box on a private tailnet — disproportionate |

## Architecture Patterns

### System Architecture Diagram

```
ASPNETCORE_ENVIRONMENT      ConnectionStrings__okozukai      ASPNETCORE_URLS
   (env var)                      (env var)                    (env var)
        │                             │                             │
        ▼                             ▼                             ▼
        └─────────────► WebApplication.CreateBuilder(args) ◄────────┘
                                      │
                                      ▼
                    IConfiguration providers resolve
                 (env vars override appsettings.json;
              double-underscore → colon: ConnectionStrings:okozukai)
                                      │
                                      ▼
                         builder.AddServiceDefaults()
                    registers "self" liveness check, tag "live"
                                      │
                        [FAIL-FAST GUARD — new, D-14]
                 if ConnectionStrings:okozukai missing/empty →
                    throw with explicit key name, before EF touched
                                      │
                                      ▼
              builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai")
           reads ConnectionStrings:okozukai · registers untagged DB health check
                                      │
                                      ▼
                              app = builder.Build()
                                      │
                                      ▼
                          app.MapDefaultEndpoints()
     ┌────────────────────────────────┴────────────────────────────────┐
     │  /health → ALL checks (self + Postgres CanConnectAsync)         │
     │  /alive  → tag=="live" only (self)                              │
     │  mapped in EVERY environment now (gate removed, D-09)           │
     └────────────────────────────────┬────────────────────────────────┘
                                      ▼
                app.Services.ApplyDatabaseMigrations()  — ALWAYS runs (D-01)
     ┌────────────────────────────────┴────────────────────────────────┐
     │  10× retry @ 3s backoff                                          │
     │  on 3D000 / "does not exist" → CREATE DATABASE, retry (D-03)    │
     │  on exhaustion → rethrow → process exits non-zero →              │
     │     Docker restart policy retries whole boot (D-02)             │
     └────────────────────────────────┬────────────────────────────────┘
                                      ▼
                       if IsDevelopment():
                  SeedDevelopmentData() + MapOpenApi()   (D-04, unchanged)
                                      ▼
        UseExceptionHandler → UseCors → (NO UseHttpsRedirection, D-06/07)
                        → UseAuthorization → MapControllers
                                      ▼
              Kestrel listens on ASPNETCORE_URLS, HTTP only (D-08)
                    e.g. http://+:8080 — no TLS in-process
```

A reader can trace the primary boot use case top-to-bottom: env vars in → config resolved → fail fast if misconfigured → health endpoints mapped → schema migrated (crash-and-restart on failure) → dev-only seed skipped in Production → HTTP pipeline serves requests with no redirect middleware.

### Recommended Project Structure
No new files/folders. Existing structure is followed:
```
src/
├── Okozukai.Api/
│   ├── Program.cs                          # D-01, D-04, D-06, D-14 land here
│   └── Properties/launchSettings.json      # dev-only, untouched (D-08)
├── Okozukai.Infrastructure/
│   └── Persistence/MigrationExtensions.cs  # unchanged — already production-grade
├── Okozukai.ServiceDefaults/
│   └── Extensions.cs                       # D-09 lands here (gate removal only)
└── Okozukai.AppHost/
    └── Program.cs                          # untouched (D-15)
.env.example                                # new file (D-16)
.gitignore                                  # already has .env entries — no change needed (see Open Questions)
```

### Pattern 1: Fail-fast configuration validation before dependent services register
**What:** Validate a required config value exists and throw a descriptive `InvalidOperationException` naming the missing key, before any service that depends on it (`AddNpgsqlDbContext`) is registered.
**When to use:** Any required environment-sourced config where a downstream failure (e.g. a retry loop) would otherwise produce many rounds of misleading errors before the real cause surfaces (D-14's exact scenario).
**Example:**
```csharp
// Pattern only — exact placement (inline vs. extension method) is Claude's discretion per D-14.
// Standard API: IConfiguration.GetConnectionString(name) reads "ConnectionStrings:{name}".
var connectionString = builder.Configuration.GetConnectionString("okozukai");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings__okozukai. " +
        "Set it as an environment variable before starting the API.");
}

builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai");
```
[CITED: learn.microsoft.com/aspnet/core/fundamentals/configuration — `IConfiguration.GetConnectionString` reads the `ConnectionStrings:{name}` section; environment variables use `__` where JSON uses `:`]

### Pattern 2: Environment-gated seed data, ungated migration
**What:** Split what used to be one `IsDevelopment()` block into: an unconditional migration call, and a still-gated seed+OpenAPI block.
**When to use:** Exactly this phase's PROD-01/PROD-02 pair — any time "run in every environment" and "run in dev only" logic get untangled from a shared guard.
**Example:**
```csharp
// Source: existing structure at src/Okozukai.Api/Program.cs:38-44, restructured per D-01/D-04
app.Services.ApplyDatabaseMigrations();

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("--> Environment is Development. Seeding data...");
    app.Services.SeedDevelopmentData();
    app.MapOpenApi();
}
```

### Pattern 3: Environment-conditional middleware removed, not made conditional
**What:** Where CONTEXT.md's D-06 calls for full removal of `UseHttpsRedirection()` (currently gated to non-Development at `Program.cs:50-53`), do not replace it with a different conditional — delete the call and the `if` block entirely.
**When to use:** When a locked topology decision (D-07: no TLS terminator anywhere) makes the middleware meaningless in every environment this app will actually run in.
**Example:**
```csharp
// BEFORE (current code, Program.cs:50-53):
// if (!app.Environment.IsDevelopment())
// {
//     app.UseHttpsRedirection();
// }
//
// AFTER: block deleted entirely. Do not add UseForwardedHeaders — no proxy in this
// milestone's topology sends X-Forwarded-Proto (D-07).
```

### Anti-Patterns to Avoid
- **Adding `UseForwardedHeaders` "just in case":** the textbook fix for this exact symptom (redirect loop behind a proxy) is `UseForwardedHeaders` + `X-Forwarded-Proto` trust. It is the *wrong* fix here because D-07 locks in a topology with no TLS-terminating proxy at all — there is nothing to forward. Adding it anyway adds header-spoofing attack surface for no benefit. [CITED: learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer]
- **Adding a `RUN_MIGRATIONS` opt-in flag:** explicitly rejected by D-01 — reintroduces the divergent-boot-path problem this phase exists to close.
- **Gating the connection-string fail-fast check to non-Development:** defeats its purpose; D-14's point is to fail identically and immediately in every environment, including local dev misconfigurations.
- **Adding a package reference for a Postgres health check:** D-10 is explicit — `AddNpgsqlDbContext` already registers one; adding e.g. `AspNetCore.HealthChecks.NpgSql` would create a duplicate, untagged check.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Waiting for Postgres to accept connections at container boot | A custom `while(!CanConnect) sleep()` loop | `MigrationExtensions.ApplyDatabaseMigrations` (10× retry, 3s backoff) — already exists, unchanged this phase | Already production-grade: structured logging, `3D000` auto-create fallback, rethrow-on-exhaustion. Rebuilding it duplicates a solved problem. |
| Postgres readiness signal for `/health` | A custom `IHealthCheck` calling `context.Database.CanConnectAsync()` | `AddNpgsqlDbContext`'s implicit `DbContextHealthCheck` (D-10) | Already registered, already untagged so it feeds `/health` but not `/alive` — the readiness/liveness split is free. |
| Composing a Postgres connection string from parts (host/port/user/pass) | Custom `POSTGRES_HOST`/`POSTGRES_USER`/etc. env vars assembled at startup | `ConnectionStrings__okozukai` single env var (D-13) | ASP.NET Core's built-in environment-variable configuration provider already converts `__` to `:` and Aspire's `AddNpgsqlDbContext("okozukai")` already reads `ConnectionStrings:okozukai` — zero new code needed to read it. |
| Trusting proxy-forwarded scheme/host headers | Manual `Request.Headers["X-Forwarded-Proto"]` parsing | `UseForwardedHeaders` + `ForwardedHeadersOptions` (built into `Microsoft.AspNetCore.HttpOverrides`) — **not needed this phase** per D-07, but the correct tool if a TLS terminator is ever introduced (Phase 2/3) | Hand-parsing forwarded headers without validating `KnownProxies`/`KnownNetworks` is a classic header-spoofing vector; the built-in middleware handles trust configuration correctly. |

**Key insight:** Every mechanism this phase's five requirements need already exists in the codebase or the framework. The work is almost entirely subtractive (remove gates, remove middleware) plus one small additive guard (D-14). Any task in the plan that proposes writing new infrastructure code for migration retries, health checks, or connection-string parsing is very likely solving an already-solved problem — check `MigrationExtensions.cs`, `Extensions.cs`, and the Aspire package docs before writing new code.

## Common Pitfalls

### Pitfall 1: `WebApplicationFactory` always forces Development — the 22 integration tests won't exercise the new "Production" boot path
**What goes wrong:** A plan step assumes the existing integration test suite validates the migration-gate change, or tries to make the tests run in "Production" mode to prove PROD-01/PROD-02.
**Why it happens:** `CustomWebApplicationFactory<TProgram>.ConfigureWebHost` calls `builder.UseEnvironment("Development")` unconditionally [VERIFIED: tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs:37 — `builder.UseEnvironment("Development");`], overriding any `ASPNETCORE_ENVIRONMENT` set externally. The same factory also swaps in `UseInMemoryDatabase("InMemoryDbForTesting")` [VERIFIED: tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs:31-34], for which `dbContext.Database.IsRelational()` returns `false`, so `ApplyDatabaseMigrations` hits its early skip branch [VERIFIED: src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs:23-27 — `if (!dbContext.Database.IsRelational()) { logger.LogInformation("--> Skip database migrations..."); break; }`] regardless of whether the call site is inside or outside the `IsDevelopment()` guard.
**How to avoid:** Confirms D-05's own reasoning is sound — automated tests genuinely cannot validate this phase's core behavior change, and the plan should not add a task that tries to force it. The manual empty-database run (D-05) is the only valid verification for PROD-01/PROD-02.
**Warning signs:** A plan task titled something like "add integration test for Production migration behavior" — this would either be a no-op (skipped by `IsRelational()`) or require introducing Testcontainers, which CONTEXT.md explicitly rules out as disproportionate for this phase.

### Pitfall 2: Fail-fast guard placed after `AddNpgsqlDbContext` defeats its own purpose
**What goes wrong:** The connection-string validation (D-14) gets added as an extension method called from inside `AddInfrastructure()` or similar, which executes *after* `builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai")` has already run at `Program.cs:22`.
**Why it happens:** `AddNpgsqlDbContext` is a DI-registration call, not a connection attempt — it doesn't itself fail on a missing connection string. But once the app reaches `ApplyDatabaseMigrations()` and tries to actually connect, a missing/empty connection string surfaces as a low-level Npgsql exception inside the 10× retry loop, producing ten rounds of retry noise before the real cause (missing env var) is visible in logs — precisely the symptom D-14 exists to prevent.
**How to avoid:** Place the guard before the `builder.AddNpgsqlDbContext<...>(...)` call, reading `builder.Configuration.GetConnectionString("okozukai")` directly rather than relying on any exception from the registration call itself.
**Warning signs:** Missing-connection-string errors in logs are Npgsql/Postgres-flavored (`Host can't be null`, DNS resolution failures, etc.) rather than the phase's own descriptive message.

### Pitfall 3: `3D000` auto-create fallback silently no-ops without `CREATEDB`
**What goes wrong:** If the Postgres role the API connects as lacks the `CREATEDB` privilege, D-03's auto-create fallback logs a failure (`"--> Database creation failed."`) [VERIFIED: src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs:49-52] and falls through to the ordinary retry/backoff path, eventually crash-looping — the exact scenario D-03 was added to prevent, but only for the "database doesn't exist yet" case, not the "role can't create it" case.
**Why it happens:** D-03 explicitly assumes `CREATEDB` "is acceptable on a single-tenant box" — this is a Phase 2 provisioning concern (the Postgres container's role setup), not something Phase 1 code can verify or control.
**How to avoid:** Not a Phase 1 action item — flag as a forward constraint for Phase 2's Postgres container provisioning: the app's role needs `CREATEDB`, or the `POSTGRES_DB` auto-creation on first container boot must be relied upon exclusively (in which case this fallback truly is just a safety net, as D-03 intends).
**Warning signs:** Migration logs show `"--> Database creation failed."` followed immediately by retries and eventual crash, on a database that genuinely doesn't exist yet.

### Pitfall 4: `launchSettings.json`'s `https` profile can leak into a manual Production verification run
**What goes wrong:** D-05's manual verification step ("start the API with `ASPNETCORE_ENVIRONMENT=Production` and env-var config") is run via `dotnet run --launch-profile https` or an IDE launch config, which applies `launchSettings.json`'s `applicationUrl` (`https://localhost:7011;http://localhost:5005`) [VERIFIED: src/Okozukai.Api/Properties/launchSettings.json:11-20], silently overriding the `ASPNETCORE_URLS` env var the test is meant to exercise.
**Why it happens:** `launchSettings.json` only applies to `dotnet run` / IDE-launched profiles, not to `dotnet <published-dll>` execution — but it's easy to reach for `dotnet run` out of habit during manual verification.
**How to avoid:** Run the D-05 verification via `dotnet run --no-launch-profile` (or the published binary directly) with `ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`, and `ConnectionStrings__okozukai` set purely as process environment variables, matching how the container will actually start it.
**Warning signs:** The manual test unexpectedly serves HTTPS, or binds to `localhost:5005`/`7011` instead of the env-var-supplied port.

### Pitfall 5: Confusing the AppHost's `ConnectionStrings:okozukai` (user secrets, dev) with the API's `ConnectionStrings__okozukai` (env var, prod)
**What goes wrong:** A plan step assumes the AppHost's `builder.AddConnectionString("okozukai")` [VERIFIED: src/Okozukai.AppHost/Program.cs:5] needs updating alongside the API's env-var change, or vice versa.
**Why it happens:** Both resolve to the same logical config key name (`okozukai`) but via completely different providers — the AppHost via user secrets (untouched, D-15) at dev time, the API via environment variables (D-13) at container run time. They are two independent configuration paths that happen to share a resource name.
**How to avoid:** D-15 is explicit: the AppHost is out of scope for this phase. Only `src/Okozukai.Api`'s configuration path changes.
**Warning signs:** A plan task touching `src/Okozukai.AppHost/Program.cs` — none of Phase 1's locked decisions require this.

## Code Examples

### Unconditional migration, dev-only seed (D-01, D-04)
```csharp
// Source: restructuring of existing src/Okozukai.Api/Program.cs:38-44
app.Services.ApplyDatabaseMigrations();

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("--> Environment is Development. Seeding data...");
    app.Services.SeedDevelopmentData();
    app.MapOpenApi();
}
```

### Health endpoint gate removed (D-09)
```csharp
// Source: src/Okozukai.ServiceDefaults/Extensions.cs:109-126 with the IsDevelopment() gate removed
public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    // All health checks must pass for app to be considered ready to accept traffic after starting
    app.MapHealthChecks(HealthEndpointPath);

    // Only health checks tagged with the "live" tag must pass for app to be considered alive
    app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("live")
    });

    return app;
}
```

### Connection-string fail-fast guard (D-14)
```csharp
// Pattern — exact location (inline vs. extension method) is Claude's discretion.
// GetConnectionString("okozukai") reads config key "ConnectionStrings:okozukai",
// which the ConnectionStrings__okozukai env var maps to via ASP.NET Core's
// built-in double-underscore convention. [CITED: learn.microsoft.com configuration docs]
var connectionString = builder.Configuration.GetConnectionString("okozukai");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings__okozukai. " +
        "Set it as an environment variable before starting the API.");
}
```

### `.env.example` shape (D-16)
```bash
# Required for the API to start in Production. Copy to .env and fill in real values;
# .env itself is gitignored (already present in .gitignore's "Environment Secrets" block).
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__okozukai=Host=postgres;Port=5432;Database=okozukai;Username=okozukai;Password=changeme
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|-------------------|---------------|--------|
| Default ASP.NET Core container port 80 | Default port 8080; new `ASPNETCORE_HTTP_PORTS` env var as a simpler alternative to `ASPNETCORE_URLS` | .NET 8 (Nov 2023) [CITED: learn.microsoft.com/dotnet/core/compatibility/containers/8.0/aspnet-port] | Non-root container images can't bind ports <1024; 8080 is now the .NET container image convention, matching D-08's chosen port. `ASPNETCORE_URLS` (used by this phase, D-08) remains fully supported for full `scheme://host:port` syntax. |
| ASP.NET Core project templates map health endpoints unconditionally | Aspire's `ServiceDefaults` template gates `/health`/`/alive` behind `IsDevelopment()` by default, with an explicit code comment pointing to `aka.ms/dotnet/aspire/healthchecks` for security guidance before removing the gate | Aspire GA-era templates (2024+) [CITED: learn.microsoft.com/dotnet/aspire/health-checks] | This phase deliberately reverses that template default (D-09) — Microsoft's own guidance for exposing health endpoints outside Development is to add caching/timeouts/host-filtering; this phase substitutes network-topology isolation (Tailnet-only, port never published, D-11) as the equivalent protection instead. This is a locked, reasoned decision — not an oversight — and should not be re-litigated by the planner. |

**Deprecated/outdated:** None specific to this phase — the codebase already targets .NET 10 and current Aspire packages (13.1.1 / 13.1.0).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|-----------------|
| A1 | The exact code shape of the fail-fast connection-string guard (`builder.Configuration.GetConnectionString("okozukai")` + `InvalidOperationException`) — the API itself is standard and documented, but this specific arrangement is illustrative, not dictated by CONTEXT.md | Code Examples, Architecture Patterns Pattern 1 | Low — D-14 explicitly leaves placement/shape to Claude's discretion; any equivalent fail-fast check before `AddNpgsqlDbContext` satisfies the requirement. |
| A2 | None — this was verified, not assumed. **Flagging here for visibility:** CONTEXT.md's D-16 states *"`.gitignore` currently has no `.env` entry — one must be added,"* but reading `.gitignore` this session shows it already contains `.env` (and four related patterns) at lines 71-77, added by commit `880a8eb` on the same day as context-gathering, after the CONTEXT.md session concluded. Do not add a task to modify `.gitignore` — only `.env.example` needs to be created. See Open Questions. | User Constraints (D-16), Open Questions | Low — creating a redundant `.gitignore` entry is harmless (git ignores duplicate patterns), but a plan task built around "add `.env` to `.gitignore`" would report success without doing anything, muddying verification. |

**If this table is empty:** N/A — one illustrative-code assumption (A1) is logged above; no claim in this research materially depends on unverified information.

## Open Questions

1. **Is D-16's `.gitignore` claim actually stale, and does it change task scope?**
   - What we know: `.gitignore` currently contains (verified this session, lines 71-77):
     ```
     # Environment Secrets
     .env
     .env.local
     .env.development.local
     .env.test.local
     .env.production.local
     *.pem
     ```
     `git log` shows this landed in commit `880a8eb` ("chore: adopt AddViteApp, fix E2E specs, ignore Playwright artifacts"), which modified `.gitignore` (+5 lines) — this commit postdates STATE.md's last-updated timestamp for the CONTEXT.md session (`2026-08-20T13:40:36Z` vs. commit time `Aug 20 23:44:38`, same day).
   - What's unclear: Whether the planner should silently drop the `.gitignore`-modification sub-task from D-16, or explicitly note in the plan that it's already satisfied (for traceability/audit purposes).
   - Recommendation: Scope D-16's implementation task to "create `.env.example`" only. If the plan-checker or verifier wants an explicit checkbox for "`.env` is gitignored," point it at the existing `.gitignore:71-77` block rather than creating a redundant edit.

2. **Does the eventual homelab Postgres role have `CREATEDB`?**
   - What we know: D-03 keeps the `3D000` auto-create fallback active in Production and explicitly assumes the app's role has `CREATEDB`, "acceptable on a single-tenant box."
   - What's unclear: The actual Postgres container/role provisioning happens in Phase 2 (compose stack, `POSTGRES_DB`/`POSTGRES_USER` env vars), which is out of scope for Phase 1. Nothing in Phase 1 can verify this.
   - Recommendation: Not a Phase 1 blocker — D-03's fallback is a safety net, not the primary path (the primary path is Postgres's own `POSTGRES_DB` auto-creating the database on first container boot). Carry forward as a note for Phase 2 planning: ensure the API's Postgres role either has `CREATEDB`, or accept that D-03's fallback is purely defensive and may not always work.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|--------------|-----------|---------|----------|
| .NET SDK | Build, run, manual Production verification (D-05) | ✓ | 10.0.100 [VERIFIED: `dotnet --version` output this session] | — |
| PostgreSQL | Manual Production verification against an empty scratch database (D-05) | ✓ | 17.10 (Homebrew), accepting connections [VERIFIED: `psql --version` and `pg_isready` output this session] | — |
| Docker | Not required for Phase 1 itself (Phase 2 scope), but useful to confirm early | ✓ | 29.6.1 [VERIFIED: `docker --version` output this session] | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None — everything Phase 1 needs is present in this environment.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|--------------------|
| V2 Authentication | No | Explicitly out of scope this milestone (Tailnet isolation is the accepted security model per `.planning/REQUIREMENTS.md` §Out of Scope) |
| V3 Session Management | No | No sessions introduced or touched by this phase |
| V4 Access Control | No | No authorization logic touched; `app.UseAuthorization()` is unchanged |
| V5 Validation, Sanitization and Encoding | Partial | D-14's fail-fast guard is itself an input-validation control on startup configuration — validate presence/non-emptiness of `ConnectionStrings__okozukai` before use |
| V7 Error Handling and Logging | Yes | D-14's error message must name the missing config key without ever logging the connection string's *value* (which contains a password per D-13); `MigrationExtensions.cs`'s existing structured logging already avoids logging the raw exception in a way that leaks the connection string |
| V9 Communication | Yes | D-06/D-07 lock in plain HTTP inside the container network, relying on Tailscale's WireGuard-based encryption at the network layer rather than in-process TLS — a deliberate, documented trade-off, not an oversight |
| V14 Configuration | Yes | Core to this phase: secrets via environment variables (D-13) not developer user-secrets, fail-fast on missing required config (D-14), health endpoints exposed based on network-topology isolation rather than app-level auth (D-11) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|------------------------|
| Connection string (with embedded password) visible via `docker inspect` | Information Disclosure | Accepted trade-off per D-13 for a single-tenant, Tailnet-only box; not mitigated in this phase. Standard mitigation for a higher-trust-boundary future would be a secrets manager or Docker secrets `_FILE` pattern — explicitly rejected here as disproportionate. |
| Unauthenticated `/health` endpoint reachable in every environment (D-09) | Information Disclosure (minor — reveals DB connectivity state) | Standard ASP.NET Core/Aspire guidance is app-level protection (auth, host-filtering, caching/timeouts) before exposing outside Development [CITED: learn.microsoft.com/dotnet/aspire/health-checks]. This phase substitutes network isolation instead: the port is never published to the host (Phase 3, ACC-03), so `/health` is only reachable on the internal container network — an explicitly reasoned deviation (D-11), not an oversight. |
| Migration retry loop producing misleading errors on missing config | Denial of Service (self-inflicted — delays failure visibility, not an external attack) | D-14's fail-fast guard is the mitigation: validate before the retry loop is ever entered. |
| A future proxy sending forged `X-Forwarded-Proto` if `UseForwardedHeaders` were ever added without `KnownProxies` configured | Spoofing | Not applicable to Phase 1 (no `UseForwardedHeaders` is added, D-07). Flag for Phase 2/3: if a TLS terminator is introduced later, `ForwardedHeadersOptions.KnownProxies`/`KnownNetworks` must be configured, not left to defaults which trust any `X-Forwarded-*` header from any source. |

## Sources

### Primary (HIGH confidence)
- Direct source reads this session (all file paths and line numbers cited inline throughout): `src/Okozukai.Api/Program.cs`, `src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs`, `src/Okozukai.ServiceDefaults/Extensions.cs`, `src/Okozukai.AppHost/Program.cs`, `src/Okozukai.Api/Properties/launchSettings.json`, `.gitignore`, `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs`, all `.csproj` files, `.planning/codebase/INTEGRATIONS.md`, `.planning/ROADMAP.md`.
- `git log --oneline -5` and `git show --stat 880a8eb` — confirmed the `.gitignore` `.env` entry landed after CONTEXT.md's session.
- `dotnet --version`, `psql --version`, `pg_isready`, `docker --version` — confirmed local toolchain availability for the manual verification step (D-05).

### Secondary (MEDIUM confidence — WebSearch cross-checked against official Microsoft Learn / Aspire docs)
- [Configuration in ASP.NET Core (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0) — double-underscore-to-colon environment variable mapping.
- [Configure ASP.NET Core to work with proxy servers and load balancers (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0) — the standard redirect-loop mechanism and the `UseForwardedHeaders` fix (confirms why it does not apply to this phase's topology).
- [Breaking change: Default ASP.NET Core port changed from 80 to 8080 (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port) — container port convention informing D-08's port choice.
- [.NET Aspire health checks (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/aspire/health-checks) — official guidance behind the `IsDevelopment()` gate that D-09 removes, and the recommended alternative protections D-11 substitutes with network isolation.
- [NuGet Gallery — Aspire.Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Aspire.Npgsql.EntityFrameworkCore.PostgreSQL) — confirms `AddNpgsqlDbContext` registers a `DbContextHealthCheck` (`CanConnectAsync`) by default, disabled only via explicit `DisableHealthChecks = true`.

### Tertiary (LOW confidence)
- None used directly — all WebSearch findings above were cross-checked against `learn.microsoft.com` or `nuget.org` as the authoritative source before inclusion.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all versions confirmed by direct `.csproj` reads this session, not registry lookup or training memory.
- Architecture: HIGH — every file/line reference verified by direct `Read` this session; CONTEXT.md's own line numbers cross-checked and found accurate (with the one `.gitignore` staleness noted).
- Pitfalls: HIGH — `CustomWebApplicationFactory`'s `UseEnvironment("Development")` and `IsRelational()` skip-branch interaction (Pitfall 1) verified by direct source read, not inference.

**Research date:** 2026-08-20
**Valid until:** 30 days (stable, config-only phase; no fast-moving dependencies)
