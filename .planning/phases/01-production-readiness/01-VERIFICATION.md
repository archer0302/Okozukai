---
phase: 01-production-readiness
verified: 2026-08-21T00:00:00Z
status: human_needed
score: 16/18 must-haves verified
behavior_unverified: 0
overrides_applied: 0
behavior_unverified_items: []
human_verification:
  - test: "Read the captured Production-boot startup log (or re-run one) and confirm it shows the migration attempt/success lines with no environment-name announcement of seeding, and no HTTPS-redirect or certificate warning anywhere in the output."
    expected: "Log contains 'Attempting to apply database migrations' / 'Migrations applied successfully' lines and zero mentions of HTTPS redirect or TLS certificate warnings."
    why_human: "PLAN.md Task 1 explicitly designates this as `<human-check>` under `human_verify_mode: end-of-phase` — the log content read is a qualitative confirmation, not a re-run of the automated assertions."
  - test: "Confirm the acceptable-risk framing for the two `verification: backstop` must-haves: (1) an interrupted mid-migration startup resumes cleanly from __EFMigrationsHistory on next boot rather than leaving a half-applied schema; (2) a connection-string password containing `;` or `=` is handled correctly via Npgsql's quoting rules at real deploy time (not exercised — .env.example only documents the plain unquoted form)."
    expected: "Human agrees these two backstop truths are acceptably deferred to Phase 2 deploy-time verification (real credential authored then) rather than needing a synthetic interruption/quoting test in Phase 1."
    why_human: "Both must-haves are marked `verification: backstop` in PLAN.md frontmatter — the planner already flagged these as non-inferable from static/dynamic checks available at this phase; per the honest-verifier contract they route to human judgment rather than being auto-resolved."
---

# Phase 1: Production Readiness Verification Report

**Phase Goal:** Okozukai behaves correctly when started with `ASPNETCORE_ENVIRONMENT=Production` — the hosting mode it will actually run in on the homelab — instead of only working under Development.

**Verified:** 2026-08-21T00:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

All truths below were checked against the actual codebase and, where they assert runtime behavior, against a **real `dotnet run` Production boot** against a local PostgreSQL instance performed live during this verification (not the SUMMARY's narration of an earlier run).

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SC-1 / PROD-01 — Production start against an empty DB applies all 6 migrations, correct schema | ✓ VERIFIED | Live boot: `select count(*) from "__EFMigrationsHistory"` = 6; ordered IDs run `20260219111426_InitialCreate` … `20260223000000_AddTransactionCreatedAt`, matching plan spec exactly. |
| 2 | SC-2 / PROD-02 — No dev seed data written in Production | ✓ VERIFIED | Live boot: `Journals`/`Tags`/`Transactions` row counts all 0; log has 0 occurrences of `Seed: populating`/`Environment is Development`. Also confirmed the pre-existing `AddJournalsPhase5` migration bug (unconditional `Default` journal insert) was fixed — verified the migration file now gates the insert behind `IF EXISTS (SELECT 1 FROM "Transactions" WHERE "Type" != 'Exchange')`. |
| 3 | SC-3 / PROD-03 — No HTTPS redirect loop over plain HTTP | ✓ VERIFIED | `Program.cs` — `grep -c UseHttpsRedirection` = 0 (block fully deleted, not gated). Live boot: `curl -w '%{http_code}|%{redirect_url}'` on `/api/journals` = `200|` (empty redirect_url, no 30x). 10 concurrent requests spanning `/health` + `/api/journals` all returned 200, none carried a redirect. |
| 4 | SC-4 / PROD-04 — Health endpoint responds outside Development, on the ordinary (non-publicly-exposed) port | ✓ VERIFIED | `Extensions.cs` — the `IsDevelopment()` wrapper around `MapDefaultEndpoints`'s two `MapHealthChecks` calls is fully removed (`awk`-scoped grep for `IsDevelopment` in that method returns 0). Live boot: `GET /health` and `GET /alive` both return `200 Healthy`; 10 concurrent `/health` requests all 200. "Internal port" is satisfied by topology per documented decision D-11 (no separate management port added), explicitly deferred to Phase 3's ACC-03 (port-publishing isolation) — this is a scoping decision recorded in the plan and threat model (T-01-02), not a gap in Phase 1. |
| 5 | SC-5 / PROD-05 — API starts with only environment variables, no user-secrets | ✓ VERIFIED | `Okozukai.Api.csproj` has no `UserSecretsId`; neither `appsettings.json` nor `appsettings.Development.json` declares a `ConnectionStrings` section; no `appsettings.Production.json` exists. `Program.cs` reads `ConnectionStrings__okozukai` via `GetConnectionString("okozukai")` exactly once, before `AddNpgsqlDbContext`, with no other configuration sources registered. Live boot with only env vars set succeeded end-to-end. |
| 6 | /health aggregates PostgreSQL check, /alive is process-only (D-12 readiness/liveness contract) | ✓ VERIFIED | `Extensions.cs` `MapDefaultEndpoints` — `/alive` uses `Predicate = r => r.Tags.Contains("live")`; only the `"self"` check (in `AddDefaultHealthChecks`) is tagged `"live"`. The implicit `AddNpgsqlDbContext` DB health check is untagged, so it's included in `/health`'s "all checks" but excluded from `/alive`'s filtered set. No duplicate health-check package added (confirmed no `.csproj` diff). |
| 7 | PROD-01 adjacency — second boot is idempotent, applies zero further migrations | ✓ VERIFIED | Live second boot against the already-migrated scratch DB: healthy in 1s, `__EFMigrationsHistory` still 6 rows, log shows 0 occurrences of "Applying migration". |
| 8 | PROD-01 empty / D-03 — 3D000 fallback creates a missing database and applies all 6 migrations | ✓ VERIFIED | Live boot against a nonexistent database name: process created the database and applied all 6 migrations automatically (`__EFMigrationsHistory` = 6 after boot); confirmed local Postgres role holds `CREATEDB` (`rolcreatedb = t`). |
| 9 | PROD-01 ordering — migration IDs list in the documented order | ✓ VERIFIED | Live query confirms the ascending order exactly matches the plan's specified sequence (6 entries, correct first/last IDs). |
| 10 | PROD-01 concurrency (backstop) — interrupted mid-migration boot resumes cleanly on next start | ⚠️ Backstop / needs human | Marked `verification: backstop` in PLAN.md frontmatter — not inferable from static or single-boot dynamic checks; simulating an interrupted migration was out of scope for this plan and this verification. Routed to human verification below. |
| 11 | PROD-03/04 concurrency — 10 concurrent /health + /api/journals requests all non-3xx, no Location header | ✓ VERIFIED | Live test: 10 concurrent requests spanning both endpoints, `sort \| uniq -c` shows all 10 returned `200` with no redirect_url populated. |
| 12 | PROD-04 concurrency — 10 concurrent /health requests all 200, no 503/DbContext reentrancy failure | ✓ VERIFIED | Live test: 10 concurrent `/health` requests, unique status set is exactly `200`. |
| 13 | PROD-05 adjacency — no UserSecretsId, no ConnectionStrings in appsettings*.json | ✓ VERIFIED | Confirmed via direct grep of `.csproj` and both `appsettings*.json` files — zero matches in both. |
| 14 | PROD-05 empty — unset/empty/whitespace connection string fails fast before migration loop, names the exact key | ✓ VERIFIED | Live test of all 3 cases (`__UNSET__`, `""`, `" "`): all exited non-zero (rc=134, unhandled `InvalidOperationException`), all logs contain exactly `ConnectionStrings__okozukai` in the message, zero occurrences of `Attempting to apply database migrations` in any of the 3 logs. Exception message: `"Missing required configuration: ConnectionStrings__okozukai. Set it as an environment variable before starting the API."` — no secret value echoed (no interpolated string in guard region). |
| 15 | PROD-05 ordering — no additional/reordered configuration sources, env vars win via ASP.NET Core defaults | ✓ VERIFIED | `Program.cs` registers no `AddJsonFile`/`AddEnvironmentVariables` calls beyond `WebApplication.CreateBuilder`'s defaults; only one `builder.Configuration.*` call in the whole file. |
| 16 | PROD-05 encoding (backstop) — a password containing `;`/`=` is handled via Npgsql quoting rules at real deploy time | ⚠️ Backstop / needs human | Marked `verification: backstop`. `.env.example`'s header comment documents the quoting-rule caveat, but the real encoding path is exercised only when a real credential is authored in Phase 2 — not testable in Phase 1 with the placeholder `changeme` value. Routed to human verification below. |
| 17 | PROD-05 concurrency — connection string read exactly once, before AddNpgsqlDbContext, never re-read/mutated while serving | ✓ VERIFIED | `grep -c 'GetConnectionString("okozukai")'` = 1; guard line (23) precedes `AddNpgsqlDbContext` (line 31); no re-read anywhere else in the file. |
| 18 | Prohibitions — no partial-schema serving, no destructive schema reconciliation, no seed contamination of real data | ✓ VERIFIED | `MigrationExtensions.cs` unmodified: rethrow-on-exhaustion (`if (retries == 0) throw;`) still active. No migration file drops/truncates ledger tables. `DevSeedData.cs`'s destructive `RemoveRange` path is unchanged and still gated behind `IsDevelopment()`. |

**Score:** 16/18 truths verified live; 2 remain in the `verification: backstop` category per the plan's own honest-verifier disposition and are routed to human review rather than counted as pass or fail.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `.env.example` | Repo root, names `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `ConnectionStrings__okozukai` | ✓ VERIFIED | Present at root, tracked (`git ls-files --error-unmatch` succeeds), all 3 keys present exactly as specified, `changeme` placeholder password, header explains quoting caveat. Real `.env` confirmed gitignored (`git check-ignore -v .env` → matches `.gitignore:72:.env`) and not tracked. |
| `src/Okozukai.Api/Program.cs` | Migration call at top level, connection-string guard before `AddNpgsqlDbContext` | ✓ VERIFIED | Read in full; matches plan's Edit A/B and Task 2 exactly (lines 23-31, 44-64). |
| `src/Okozukai.ServiceDefaults/Extensions.cs` | `MapDefaultEndpoints` maps both health routes unconditionally | ✓ VERIFIED | Read in full; `IsDevelopment()` wrapper fully removed from `MapDefaultEndpoints` (lines 109-121). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `ConnectionStrings__okozukai` (env) | `builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai")` | double-underscore config mapping | ✓ WIRED | Confirmed by live boot succeeding with only the env var set, and by the fail-fast guard correctly reading the same key via `GetConnectionString("okozukai")`. |
| `app.Services.ApplyDatabaseMigrations()` | `__EFMigrationsHistory` | top-level unconditional call in `Program.cs` | ✓ WIRED | Confirmed by live boot: 6 migrations applied to a brand-new DB with no environment gate involved. |
| `app.MapDefaultEndpoints()` | `/health`, `/alive` routes | `ServiceDefaults/Extensions.cs` | ✓ WIRED | Confirmed by live `curl` responses in Production mode. |
| `AddNpgsqlDbContext`'s implicit DB health check | `/health` (included) vs `/alive` (excluded via `live`-tag predicate) | health-check tagging | ✓ WIRED | Confirmed by reading `AddDefaultHealthChecks` (only `"self"` tagged `"live"`) and `MapDefaultEndpoints`'s predicate. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds | `dotnet build Okozukai.slnx -nologo` | 0 errors, 40 pre-existing NuGet-advisory warnings (unrelated to this phase) | ✓ PASS |
| Full test suite (31 unit + 22 integration) | `dotnet test Okozukai.slnx --no-build -nologo` | 53/53 passed | ✓ PASS |
| Production boot, empty DB, all 6 migrations | live `dotnet run` against scratch Postgres DB | `/health`=200 Healthy, `/alive`=200 Healthy, 6 migrations, 0 seed rows | ✓ PASS |
| No redirect loop over plain HTTP | `curl -w '%{http_code}\|%{redirect_url}' /api/journals` | `200\|` (empty) | ✓ PASS |
| 10-way concurrency across `/health` + `/api/journals` | parallel curl | all `200`, no redirects | ✓ PASS |
| Idempotent second boot | live re-run against same migrated DB | 6 rows unchanged, 0 "Applying migration" lines | ✓ PASS |
| 3D000 auto-create fallback | live run against a nonexistent DB name | DB auto-created, 6 migrations applied | ✓ PASS |
| Fail-fast guard: unset/empty/whitespace connection string | live run x3 | all rc≠0 (134), message names key, 0 migration-loop log lines | ✓ PASS |
| `git status --short` clean apart from planning artifacts | `git status --short` | only `.planning/config.json` (M) and untracked `.planning/research/`, `.gsd/` — both pre-existing/unrelated to this phase's diff | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| PROD-01 | 01-01-PLAN.md | Automatic migration on Production start | ✓ SATISFIED | Live boot verification (truths 1, 7-9). |
| PROD-02 | 01-01-PLAN.md | No dev seed data outside Development | ✓ SATISFIED | Live boot verification + migration bug fix (truth 2). |
| PROD-03 | 01-01-PLAN.md | No redirect loop over plain HTTP | ✓ SATISFIED | Live boot + concurrency verification (truths 3, 11). Implemented by deleting `UseHttpsRedirection()` outright (D-06/D-07) rather than adding forwarded-header trust — a documented, deliberate scope decision since no TLS terminator exists anywhere in this milestone's topology; satisfies the roadmap's literal SC-3 wording ("serves requests without entering a redirect loop"). |
| PROD-04 | 01-01-PLAN.md | Health endpoints respond outside Development, internal port | ✓ SATISFIED | Live boot + concurrency verification (truths 4, 12). "Internal port" satisfied by topology, explicitly deferred to Phase 3 ACC-03 per D-11 — documented, not silently dropped. |
| PROD-05 | 01-01-PLAN.md | Env-var-only configuration, no user-secrets | ✓ SATISFIED | Static + live verification (truths 5, 13-17). |

REQUIREMENTS.md traceability table already marks all five as "Complete" under Phase 1 — consistent with the evidence found. No orphaned requirements: all 5 PROD-* IDs declared in the PLAN frontmatter match the 5 IDs mapped to Phase 1 in REQUIREMENTS.md's traceability table exactly.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs` | 18-23 | Comment/string contains "placeholder" | ℹ️ Info | Not a stub — this is a legitimate, documented test-harness workaround (SUMMARY's deviation #2) for the eager fail-fast guard's execution order relative to `WebApplicationFactory`. The value is never dialed; `ConfigureServices` swaps in an in-memory DbContext immediately after. No `TBD`/`FIXME`/`XXX`/`HACK` markers found in any file modified by this phase. |

No blocking anti-patterns found in any of the four files this phase modified.

### Human Verification Required

### 1. Read the captured Production-boot startup log

**Test:** Read the startup log from a Production boot (a fresh capture was made during this verification: 6 migrations applying, health check green, no seed/redirect lines) and confirm qualitatively that it shows migration attempt/success lines with no environment-name seeding announcement and no HTTPS-redirect/certificate warning anywhere.
**Expected:** Log narrative reads clean — migrations applied, no seeding, no TLS/redirect noise.
**Why human:** PLAN.md Task 1 explicitly designates this as a deferred `<human-check>` under `human_verify_mode: end-of-phase` — it is a qualitative log read, not a re-runnable automated assertion. (This verifier's live rerun already confirms the *content* the log check is asking about is correct — this item exists for the human sign-off step the plan itself requires.)

### 2. Accept the two backstop must-haves as adequately deferred

**Test:** Confirm that (a) an interrupted mid-migration Production startup is acceptably left unverified in Phase 1 given the single-instance milestone constraint and EF Core's built-in migration resumability, and (b) a password containing Npgsql delimiter characters (`;`/`=`) is acceptably deferred to real Phase 2 deploy-time verification rather than tested here with a synthetic credential.
**Expected:** Human agrees both are reasonable scope boundaries, not gaps.
**Why human:** Both are marked `verification: backstop` in PLAN.md's own frontmatter — the plan itself flags these as non-inferable by static or dynamic checks available at this phase, per the honest-verifier fallback protocol (they must never be silently auto-resolved as VERIFIED).

### Gaps Summary

No gaps found. All 16 non-backstop must-have truths were independently re-verified against a live `dotnet run` Production boot (build, full test suite, migrations, seed absence, redirect absence, health-check reachability and concurrency, fail-fast guard, and env-var-only configuration) rather than trusting SUMMARY.md's narration of an earlier run. Source-level assertions (grep patterns, git diff scoping, artifact existence) all match the plan's acceptance criteria exactly. The two remaining items are the plan's own explicitly-declared `verification: backstop` truths, which the honest-verifier protocol requires routing to human judgment rather than auto-passing or auto-failing — this is why overall status is `human_needed` rather than `passed`, even though every checkable truth passed.

---

*Verified: 2026-08-21T00:00:00Z*
*Verifier: Claude (gsd-verifier)*
