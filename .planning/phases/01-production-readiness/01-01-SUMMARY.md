---
phase: 01-production-readiness
plan: 01
subsystem: infra
tags: [aspnetcore, dotnet10, postgres, ef-core-migrations, health-checks, environment-config]

# Dependency graph
requires: []
provides:
  - "Unconditional database migration on API startup in every environment (PROD-01)"
  - "Dev-only seed data, guaranteed empty ledger on a fresh Production boot (PROD-02)"
  - "No HTTPS-redirect loop over plain HTTP (PROD-03)"
  - "/health and /alive mapped and reachable outside Development (PROD-04)"
  - "Fail-fast startup when ConnectionStrings__okozukai is missing/empty/whitespace (PROD-05)"
  - "Committed .env.example documenting the three required deployment env vars"
affects: [02-packaging, 03-tailnet-access]

# Actuals (#2632)
actuals:
  tokens: 2175
  tasks: 3
  commits: 4

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fail-fast configuration guard as a plain top-level statement before the dependent DI registration (no custom exception type, no validation framework)"
    - "IsDevelopment() gate removed by deletion, not inversion, to preserve git-blame-friendly diffs"

key-files:
  created:
    - .env.example
  modified:
    - src/Okozukai.Api/Program.cs
    - src/Okozukai.ServiceDefaults/Extensions.cs
    - src/Okozukai.Infrastructure/Persistence/Migrations/20260222120000_AddJournalsPhase5.cs
    - tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs

key-decisions:
  - "Fixed a pre-existing migration bug (AddJournalsPhase5 unconditionally inserted a 'Default' journal on every fresh database) rather than leaving it — it directly violated PROD-02/SC-2 and this migration has never been applied to any real production database, so the fix is safe."
  - "Placed the connection-string guard inline in Program.cs (not an extension method) — no AddConfigurationValidation()-style helper exists elsewhere in the codebase to mimic."
  - "Port 8080 for ASPNETCORE_URLS in .env.example — the .NET container-image default since .NET 8; Claude's discretion per D-08, nothing depends on it until Phase 2."
  - "Did not create appsettings.Production.json — appsettings.json's existing log levels are already sane, and an empty environment-specific file would reintroduce a per-environment configuration surface for no benefit."
  - "Did not edit .gitignore — the '.env' entry already existed (commit 880a8eb, after CONTEXT.md's session), so D-16's .gitignore half was already satisfied; verified via git check-ignore rather than re-adding a redundant pattern."

patterns-established:
  - "Environment-gated seed data, ungated migration: app.Services.ApplyDatabaseMigrations() unconditional; SeedDevelopmentData()/MapOpenApi() stay inside if (app.Environment.IsDevelopment())."
  - "Test-harness placeholder env vars: CustomWebApplicationFactory's static constructor sets a fake ConnectionStrings__okozukai before host build, since eager Program.cs top-level guards run before WebApplicationFactory's ConfigureWebHost overrides can take effect."

requirements-completed: [PROD-01, PROD-02, PROD-03, PROD-04, PROD-05]

coverage:
  - id: D1
    description: "Production start against an empty/missing database applies all six migrations (idempotent on second boot, 3D000 auto-create fallback works)"
    requirement: "PROD-01"
    verification:
      - kind: other
        ref: "Production boot verification script (Task 1 <verify> block) — real dotnet run against local PostgreSQL 17, asserting __EFMigrationsHistory row count and migration ID ordering"
        status: pass
    human_judgment: false
  - id: D2
    description: "No development seed data written on a Production boot — Journals/Tags/Transactions all zero rows, including the fixed AddJournalsPhase5 'Default' journal insert"
    requirement: "PROD-02"
    verification:
      - kind: other
        ref: "Production boot verification script — psql row counts on Journals/Tags/Transactions = 0, zero 'Seed: populating' log lines"
        status: pass
    human_judgment: false
  - id: D3
    description: "No HTTPS redirect loop — plain-HTTP requests return 200, never a 30x, single and under 10-way concurrency"
    requirement: "PROD-03"
    verification:
      - kind: other
        ref: "Production boot verification script — curl redirect_url empty, concurrent xargs -P10 status-code assertions"
        status: pass
    human_judgment: false
  - id: D4
    description: "/health and /alive both return 200 Healthy under ASPNETCORE_ENVIRONMENT=Production, single and under concurrency"
    requirement: "PROD-04"
    verification:
      - kind: other
        ref: "Production boot verification script — curl body checks and 10-way concurrent health checks"
        status: pass
    human_judgment: false
  - id: D5
    description: "Missing/empty/whitespace ConnectionStrings__okozukai fails fast with a message naming the exact key, before the migration retry loop is entered; a valid value still boots normally"
    requirement: "PROD-05"
    verification:
      - kind: other
        ref: "Fail-fast guard verification script (Task 2 <verify> block) — three misconfiguration cases + re-run of the Production boot script confirming good input still works"
        status: pass
    human_judgment: false
  - id: D6
    description: ".env.example committed at repo root documenting all three required env vars; real .env confirmed gitignored"
    requirement: "PROD-05"
    verification:
      - kind: other
        ref: ".env.example content-assertion checks (Task 3 <verify> block) — grep checks via hash comparison due to .env* read-deny policy, git check-ignore/ls-files checks"
        status: pass
    human_judgment: false

# Metrics
duration: ~30min
completed: 2026-08-21
status: complete
---

# Phase 1 Plan 1: Production Startup Correctness Summary

**Okozukai's API now boots correctly under `ASPNETCORE_ENVIRONMENT=Production`: migrations run unconditionally, seed data stays dev-only, the HTTPS-redirect loop is gone, health checks are exposed everywhere, and a fail-fast guard catches a missing connection string before it reaches the migration retry loop — all proven by a real Production boot against an empty PostgreSQL database.**

## Performance

- **Duration:** ~30 min
- **Completed:** 2026-08-21
- **Tasks:** 3
- **Files modified:** 5 (4 modified, 1 created)

## Accomplishments

- `ApplyDatabaseMigrations()` moved out of the `IsDevelopment()` guard so migrations apply in every environment; `SeedDevelopmentData()`/`MapOpenApi()` remain dev-only (PROD-01, PROD-02)
- `UseHttpsRedirection()` deleted entirely — this topology has no TLS terminator anywhere, so the standard `UseForwardedHeaders` fix doesn't apply; removal is the correct fix (PROD-03)
- `MapDefaultEndpoints`'s `IsDevelopment()` gate removed so `/health` and `/alive` map in every environment, on the ordinary API port (PROD-04)
- A fail-fast `InvalidOperationException` guard added before `AddNpgsqlDbContext`, naming `ConnectionStrings__okozukai` explicitly without ever echoing the secret value (PROD-05, D-14)
- `.env.example` committed at the repo root with exactly the three required deployment variables; the real `.env` confirmed already gitignored, no redundant edit made
- **Discovered and fixed a genuine pre-existing bug**: the `AddJournalsPhase5` migration unconditionally inserted a `Default` journal into every fresh database (not just databases with legacy data to migrate), which would have silently violated PROD-02/SC-2 on the very first homelab deployment
- Full end-to-end Production boot verified against a real local PostgreSQL instance: schema (6 migrations, correct ordering), zero seed rows, no redirects, health checks under 10-way concurrency, idempotent second boot, and the `3D000` auto-create-database fallback — all passing

## Task Commits

Each task was committed atomically, plus one follow-up deviation-fix commit:

1. **Task 1: End-to-end Production boot — one path only** - `31718aa` (feat) — includes the migration bug fix (deviation, see below)
2. **Task 2: Fail fast when the connection string is absent** - `1de2732` (feat)
   - Follow-up deviation fix - `f5d0e01` (fix) — test-harness placeholder connection string, required because Task 2's guard broke all 22 integration tests
3. **Task 3: Commit the deployment environment contract** - `b566bec` (feat)

## Files Created/Modified

- `src/Okozukai.Api/Program.cs` — migration call relocated out of the environment guard; HTTPS-redirect block deleted; fail-fast connection-string guard added before `AddNpgsqlDbContext`
- `src/Okozukai.ServiceDefaults/Extensions.cs` — `IsDevelopment()` gate removed from `MapDefaultEndpoints`, so `/health`/`/alive` map in every environment
- `src/Okozukai.Infrastructure/Persistence/Migrations/20260222120000_AddJournalsPhase5.cs` — Default-journal insert and Transactions backfill now conditional on pre-existing transaction data (deviation fix)
- `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs` — static constructor supplies a placeholder `ConnectionStrings__okozukai` env var so the new fail-fast guard doesn't break the 22 integration tests (deviation fix)
- `.env.example` (new) — documents `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `ConnectionStrings__okozukai` for Phase 2's compose `env_file`

## Decisions Made

- Fixed the `AddJournalsPhase5` migration bug in place rather than leaving it or writing a new migration to undo it — it has never been applied to any real production database (this phase is the first production deployment ever), so editing its `Up()` method is safe and is exactly the right time to do it.
- Placed the connection-string guard inline as a top-level statement, matching the file's existing registration style — no `AddConfigurationValidation()`-style helper exists anywhere in the codebase to mimic (per D-14's discretion note).
- Chose port `8080` for `ASPNETCORE_URLS` in `.env.example` — the .NET container-image default since .NET 8; Claude's discretion per D-08, nothing depends on it until Phase 2 fixes the deployment contract.
- Did not create `appsettings.Production.json` — `appsettings.json`'s existing log levels (`Default: Information`, `Microsoft.AspNetCore: Warning`) are already sane; an empty environment-specific file would reintroduce a per-environment configuration surface for no benefit.
- Did not edit `.gitignore` — verified the `.env` entry already exists (landed in commit `880a8eb`, after CONTEXT.md's session concluded), so D-16's `.gitignore` half was already satisfied. Verified with `git check-ignore -v .env` rather than adding a redundant pattern.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed unconditional "Default" journal insert in `AddJournalsPhase5` migration**
- **Found during:** Task 1 (Production boot verification)
- **Issue:** The pre-existing `20260222120000_AddJournalsPhase5` migration's `Up()` method unconditionally inserts a `Default` journal (`INSERT INTO "Journals" ...`) into every database the migration runs against, regardless of whether any legacy transactions exist to migrate. Its own comment says it's meant to backfill "existing transactions" — but for a brand-new, truly empty database (exactly this phase's target scenario), it silently writes one spurious row, directly violating the plan's own acceptance criterion and PROD-02/SC-2 ("no development seed data is written" / zero rows in `Journals` after a Production boot). Discovered when the Task 1 boot-verification script failed with "Journals not empty" against a freshly created scratch database.
- **Fix:** Wrapped the currency lookup, `Default` journal insert, and `Transactions` backfill in `IF EXISTS (SELECT 1 FROM "Transactions" WHERE "Type" != 'Exchange') THEN ... END IF;`, matching the migration's own documented intent (only backfill when there's legacy data to backfill). The `ALTER TABLE ADD COLUMN`, `DELETE FROM "Transactions" WHERE "Type" = 'Exchange'`, and `ALTER COLUMN ... SET NOT NULL` statements remain unconditional (no-ops on an empty table, correct regardless of data volume).
- **Files modified:** `src/Okozukai.Infrastructure/Persistence/Migrations/20260222120000_AddJournalsPhase5.cs`
- **Verification:** Re-ran the full Production boot verification script against a freshly created scratch database — `Journals`/`Tags`/`Transactions` all return `count(*) = 0`; re-ran `dotnet test` (all 53 tests still pass, confirming the developer's already-migrated `okozukai` database is unaffected since EF Core never re-runs an already-applied migration).
- **Committed in:** `31718aa` (part of the Task 1 commit)

**2. [Rule 3 - Blocking] Supplied a placeholder connection string for the integration test host**
- **Found during:** Task 2 (fail-fast connection-string guard implementation)
- **Issue:** Task 2's new eager guard (`GetConnectionString("okozukai")` check before `AddNpgsqlDbContext`) runs as a top-level `Program.cs` statement, which — per direct evidence from the resulting stack trace (`DeferredHostBuilder.Build()` → `Program.<Main>$` throwing at the guard line) — executes *before* `CustomWebApplicationFactory.ConfigureWebHost`'s `ConfigureServices` override (which swaps `OkozukaiDbContext` to an in-memory provider) ever takes effect. With no `ConnectionStrings:okozukai` configured anywhere in the test project, the guard threw on every test, failing all 22 integration tests — exactly the regression the plan's own "No regression" acceptance criterion warned against, but empirically the "correctly placed" guard the plan assumed would avoid this did not, given this codebase's actual test-factory mechanics.
- **Fix:** Added a static constructor to `CustomWebApplicationFactory<TProgram>` that sets a placeholder `ConnectionStrings__okozukai` process environment variable (`Host=localhost;...;Password=test`) before any host builds. The guard sees a non-empty value and passes; the placeholder is never actually dialed, since `ConfigureServices` immediately swaps `OkozukaiDbContext` to `UseInMemoryDatabase` afterward.
- **Files modified:** `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs`
- **Verification:** All 53 tests pass (31 unit + 22 integration); re-ran the Task 2 fail-fast verification script (three misconfiguration cases still correctly fail fast) and the Task 1 Production boot script (a valid connection string still boots normally) to confirm the fix didn't weaken the guard itself.
- **Committed in:** `f5d0e01` (separate commit immediately after `1de2732`, kept isolated from the Task 2 guard commit so `git diff --name-only 1de2732~1 1de2732` still shows only `Program.cs`, matching the plan's literal Task 2 acceptance check)

---

**Total deviations:** 2 auto-fixed (1 bug fix in a pre-existing migration, 1 blocking test-infrastructure fix)
**Impact on plan:** Both fixes were necessary for the plan's own acceptance criteria to hold (PROD-02/SC-2's zero-seed-data guarantee, and the "no regression" test-suite requirement). No scope creep — no new features, no schema changes, no additional packages.

## Issues Encountered

- Two earlier attempts at the Task 1 boot-verification script failed for environmental reasons unrelated to the code changes: (1) `xargs -I_` collided with the literal underscore in curl's `%{http_code}` format string, corrupting the concurrency checks — fixed by using a non-colliding placeholder (`-IREPL`); (2) a leftover `dotnet run` process from an earlier failed script attempt held an open connection to the scratch database, so `dropdb --if-exists` silently no-op'd and the "fresh" database wasn't actually fresh — fixed by explicitly killing the stray process before recreating the database. Neither affected the final, clean verification run recorded above.

## User Setup Required

None - no external service configuration required. `.env.example` is a template for Phase 2's compose stack; no real secrets were created or need configuring in this phase.

## Next Phase Readiness

- Phase 2 (packaging) can now build on a Production-correct API: migrations, health checks, and connection-string handling all behave identically to how they'll run in a container.
- **Carried forward for Phase 2:** the app's PostgreSQL role must hold `CREATEDB` for the `3D000` auto-create fallback to work, or provisioning must rely exclusively on `POSTGRES_DB` auto-creation on first container boot — unverifiable from Phase 1's local Homebrew Postgres, where the developer's role is already a superuser.
- **Carried forward for Phase 2:** `/health` = ready (includes the PostgreSQL check), `/alive` = live (process-only `self` check) — this is the exact contract the Dockerfile `HEALTHCHECK` and any `depends_on: condition: service_healthy` must consume.
- **Carried forward for Phase 3:** if a TLS terminator is ever introduced, `ForwardedHeadersOptions.KnownProxies`/`KnownNetworks` must be configured explicitly — today there is deliberately no forwarded-header trust configured (D-07).
- **Flagged for human review (not a Phase 1 blocker):** `DevSeedData.SeedDevelopmentData` still calls `RemoveRange` on all tags and journals before writing, guarded only by "zero transactions." A real database holding journals/tags but no transactions would be wiped by an accidental Development start against it. Unchanged by this phase; worth a follow-up requirement if judged risky enough.
- No blockers for Phase 2.

---
*Phase: 01-production-readiness*
*Completed: 2026-08-21*

## Self-Check: PASSED

- FOUND: `.env.example`
- FOUND: `.planning/phases/01-production-readiness/01-01-SUMMARY.md`
- FOUND: `31718aa` (Task 1 commit)
- FOUND: `1de2732` (Task 2 commit)
- FOUND: `f5d0e01` (deviation-fix commit)
- FOUND: `b566bec` (Task 3 commit)
- FOUND: `f525086` (SUMMARY.md commit)
