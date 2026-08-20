---
phase: 01-production-readiness
reviewed: 2026-08-21T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - .env.example
  - src/Okozukai.Api/Program.cs
  - src/Okozukai.Infrastructure/Persistence/Migrations/20260222120000_AddJournalsPhase5.cs
  - src/Okozukai.ServiceDefaults/Extensions.cs
  - tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs
findings:
  critical: 1
  warning: 3
  info: 1
  total: 5
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-08-21T00:00:00Z
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Reviewed the four accessible source/config files for the production-readiness phase.
`.env.example` was in scope per the workflow config but the sandbox's file-access
permission settings denied both the `Read` tool and `Bash cat`/`grep` against it (it
was blocked outright, not merely truncated). It is listed in `files_reviewed_list` to
preserve scope for a follow-up pass, but **its contents were not actually inspected in
this review** — flag this gap to the orchestrator/human so `.env.example` gets a
manual pass (check for accidentally-real secrets, missing required keys, or stale
variable names) before this phase is signed off.

Of the four files actually reviewed: `Program.cs`'s fail-fast connection-string guard,
the Aspire `ServiceDefaults` wiring, and the migration's `PROD-02` fresh-database
guard (zero rows written to a brand-new DB) are all correctly implemented and
well-commented — the "production readiness" intent comes through clearly. However,
the `AddJournalsPhase5` migration has a genuine data-loss/rollback-safety defect, the
API's CORS policy is unnecessarily broad for a phase explicitly about hardening
production posture, a startup log line bypasses the OpenTelemetry logging pipeline
that this same phase sets up, and the integration test factory has a latent
test-isolation bug that will surface as soon as a second integration test class is
added.

## Critical Issues

### CR-01: Migration deletes financial transactions with no backup, and its `Down()` silently corrupts rather than restores data

**File:** `src/Okozukai.Infrastructure/Persistence/Migrations/20260222120000_AddJournalsPhase5.cs:65` (delete) and `:100-106` (broken rollback)

**Issue:** `Up()` permanently deletes every `Exchange`-type transaction row (`DELETE FROM "Transactions" WHERE "Type" = 'Exchange';`, line 65) with no export/backup step, and unconditionally drops the per-transaction `Currency`, `ExchangeToAmount`, `ExchangeToCurrency`, and `ExchangeFeeAmount` columns (lines 88-93) — this is real, unrecoverable financial data loss for any existing production database that has Exchange transactions or per-transaction currency values that differ from the newly-computed journal `PrimaryCurrency`.

Worse, `Down()` (lines 97-142) does not actually reverse this: it re-adds the `Currency` column with `defaultValue: "USD"` for **every** existing row (lines 100-106), regardless of what each transaction's original currency actually was, and it does not restore any of the deleted `Exchange` rows. An operator who runs `dotnet ef database update <previous>` to roll back a bad deploy will see the rollback "succeed" while every transaction's currency has silently been rewritten to `USD` and all exchange history is permanently gone — there is no error, warning, or log message indicating the rollback is lossy. A `Down()` that cannot faithfully invert its `Up()` should fail loudly (throw) rather than pretend to succeed with corrupted data.

**Fix:** At minimum, make the irreversibility explicit and safe:
```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    throw new NotSupportedException(
        "AddJournalsPhase5 is irreversible: it permanently deletes Exchange " +
        "transactions and per-transaction Currency values. Restore from a " +
        "pre-migration backup instead of rolling back.");
}
```
If rollback support is actually required, the migration needs a pre-delete archival
step (e.g., `INSERT INTO "Transactions_Archive_Phase5" SELECT * FROM "Transactions" WHERE "Type" = 'Exchange'` before the `DELETE`, and a real per-row currency backfill in `Down()`), plus an operational runbook note that this migration should only run after a verified backup.

## Warnings

### WR-01: CORS policy allows any origin, method, and header

**File:** `src/Okozukai.Api/Program.cs:13-21`

**Issue:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```
This default policy applies to every controller (`app.UseCors()` at line 58 uses no named policy). It lets any website a user's browser visits make cross-origin requests to this personal-finance API and read the response (no credentials are involved, so browsers won't block it). For a phase specifically about production readiness, this should be scoped to the known frontend origin(s) rather than left wide open — especially since there is no authentication layer in this codebase to fall back on as a second line of defense.

**Fix:**
```csharp
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("Missing required configuration: Cors__AllowedOrigins.");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

### WR-02: Startup log line bypasses the OpenTelemetry logging pipeline

**File:** `src/Okozukai.Api/Program.cs:51`

**Issue:** `Console.WriteLine("--> Environment is Development. Seeding data...");` writes directly to stdout instead of going through `ILogger`/`app.Logger`. Every other startup log message in this codebase (`MigrationExtensions`, `DevSeedData`) goes through `ILogger`, which `ServiceDefaults.Extensions.ConfigureOpenTelemetry` wires up for structured, exported logging. This one line silently opts out of that pipeline (no trace correlation, not exported to whatever OTLP backend/log aggregator production uses), which undermines the observability work this same phase is establishing.

**Fix:**
```csharp
if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("--> Environment is Development. Seeding data...");
    app.Services.SeedDevelopmentData();
    app.MapOpenApi();
}
```

### WR-03: Integration test factory hard-codes a shared in-memory database name

**File:** `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs:43-46`

**Issue:**
```csharp
services.AddDbContext<OkozukaiDbContext>(options =>
{
    options.UseInMemoryDatabase("InMemoryDbForTesting");
});
```
EF Core's in-memory provider shares database state across all `DbContext` instances that use the same database name within the same test process. Only one test class (`TransactionsApiTests`) currently uses this factory, so there's no observable failure yet, but xUnit runs distinct test classes in parallel by default. As soon as a second `IClassFixture<CustomWebApplicationFactory<Program>>` test class is added (very likely for a phase adding Journals/Tags integration coverage), both factory instances will share the same named in-memory database, causing cross-test data leakage and intermittent, hard-to-diagnose failures (unique-constraint violations, unexpected row counts, flaky assertions).

**Fix:** Use a unique database name per factory instance:
```csharp
services.AddDbContext<OkozukaiDbContext>(options =>
{
    options.UseInMemoryDatabase($"InMemoryDbForTesting-{Guid.NewGuid()}");
});
```

## Info

### IN-01: Variable shadowing in `ConfigureOpenTelemetry`

**File:** `src/Okozukai.ServiceDefaults/Extensions.cs:62-73`

**Issue:** The outer `WithTracing(tracing => ...)` lambda parameter (`TracerProviderBuilder tracing`) is shadowed by the inner `AddAspNetCoreInstrumentation(tracing => tracing.Filter = ...)` lambda parameter (`AspNetCoreTraceInstrumentationOptions tracing`). It compiles fine and is stock Aspire-template boilerplate, but the reused name makes the block harder to scan and risks an accidental reference to the wrong `tracing` if the block is extended later.

**Fix:** Rename the inner parameter for clarity:
```csharp
.AddAspNetCoreInstrumentation(aspNetCoreOptions =>
    aspNetCoreOptions.Filter = context =>
        !context.Request.Path.StartsWithSegments(HealthEndpointPath)
        && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
)
```

---

_Reviewed: 2026-08-21T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
