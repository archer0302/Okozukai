# Phase 1: Production Readiness - Pattern Map

**Mapped:** 2026-08-21
**Files analyzed:** 5 (4 modified, 1 new; `.gitignore` scoped out — see Assumption below)
**Analogs found:** 5 / 5 (all analogs are the files themselves — this phase is self-contained edits, not new-pattern-from-elsewhere work)

**Framing note:** Per RESEARCH.md, this phase is almost entirely *subtractive/relocative* — remove `IsDevelopment()` gates, delete `UseHttpsRedirection()` — plus one small additive fail-fast guard (D-14) and one new file (`.env.example`). There is no other module in this codebase that does "gated startup code" differently, so the closest analog for every touched file is **the file itself, before/after**, with the surrounding lines the executor must preserve. Two nearby DI-extension files (`Okozukai.Infrastructure/DependencyInjection.cs`, `GlobalExceptionHandler.cs`) are included as **style analogs** only for the one genuinely new code fragment (D-14's guard), to keep it idiomatic with the rest of the codebase.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/Okozukai.Api/Program.cs` (migration/seed block, lines 38-44) | config/bootstrap | request-response (startup, one-time) | itself — current lines 38-44 | exact (self, relocation) |
| `src/Okozukai.Api/Program.cs` (HTTPS redirect block, lines 50-53) | middleware | request-response | itself — current lines 50-53 | exact (self, deletion) |
| `src/Okozukai.Api/Program.cs` (fail-fast guard, new, ~before line 22) | config/bootstrap | request-response (startup validation) | `src/Okozukai.Infrastructure/DependencyInjection.cs` (style), `MigrationExtensions.cs` throw pattern | role-match (new code, no direct in-file precedent) |
| `src/Okozukai.ServiceDefaults/Extensions.cs` (`MapDefaultEndpoints`, lines 109-126) | middleware/route registration | request-response | itself — current lines 109-126 | exact (self, gate removal) |
| `.env.example` (new file) | config | file-I/O (static, read at deploy time) | none in repo (no prior `.env*` file exists) | no analog — use RESEARCH.md shape (D-16) |
| `.gitignore` | config | file-I/O | N/A — already satisfies D-16 (verified lines 71-77); **no change needed**, do not schedule as a task | already satisfied |

## Pattern Assignments

### `src/Okozukai.Api/Program.cs` — migration/seed relocation (D-01, D-04)

**Analog:** the file itself, current state (read in full this session).

**Current shape (lines 37-44) — what must change:**
```csharp
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("--> Environment is Development. Attempting database migration...");
    app.Services.ApplyDatabaseMigrations();
    app.Services.SeedDevelopmentData();
    app.MapOpenApi();
}
```

**Target shape (per RESEARCH.md Pattern 2 / Code Examples):**
```csharp
app.Services.ApplyDatabaseMigrations();

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("--> Environment is Development. Seeding data...");
    app.Services.SeedDevelopmentData();
    app.MapOpenApi();
}
```

**What to preserve:** the block sits between `app.MapDefaultEndpoints();` (line 35) and `app.UseExceptionHandler();` (line 46) — do not reorder relative to those two calls. `ApplyDatabaseMigrations()` is called via `app.Services.` (an `IServiceProvider` extension method), matching how `SeedDevelopmentData()` is already called — same calling convention, just unindented and pulled above the `if`.

---

### `src/Okozukai.Api/Program.cs` — HTTPS redirect removal (D-06)

**Analog:** the file itself, current lines 46-55.

**Current shape (lines 46-53):**
```csharp
app.UseExceptionHandler();

app.UseCors();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
```

**Target shape:**
```csharp
app.UseExceptionHandler();

app.UseCors();

app.UseAuthorization();
```

**What to preserve:** delete the `if (!app.Environment.IsDevelopment()) { app.UseHttpsRedirection(); }` block in its entirety — do not replace with `UseForwardedHeaders` (explicitly rejected, D-07). `UseExceptionHandler()` → `UseCors()` → `UseAuthorization()` → `MapControllers()` ordering is otherwise untouched.

---

### `src/Okozukai.Api/Program.cs` — fail-fast connection-string guard (D-14, new code)

**No direct in-file or in-repo analog** — this is new startup-validation code. Style/idiom analogs:

**Style analog 1 — throw-with-descriptive-message convention** (`src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs:34-58`, read in full this session): the codebase's existing convention for startup-critical failures is to log first, then throw a plain exception (no custom exception type) that surfaces up through the host. The guard should follow the same "plain, descriptive, no custom type" idiom — an `InvalidOperationException` naming the exact missing config key, per RESEARCH.md Pattern 1:
```csharp
var connectionString = builder.Configuration.GetConnectionString("okozukai");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings__okozukai. " +
        "Set it as an environment variable before starting the API.");
}

builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai");
```

**Style analog 2 — placement relative to registration calls** (`src/Okozukai.Api/Program.cs:12-28`, existing top-level builder-configuration block): all `builder.Services.Add*` / `builder.Add*` calls are top-level statements in registration order with no wrapping method — the guard should be inserted the same way, as a plain statement immediately before `builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai");` (currently line 22), not wrapped in a new extension method (per D-14's discretion note, inline is consistent with how the rest of `Program.cs` is written — there is no `AddConfigurationValidation()`-style helper anywhere in the codebase to mimic).

**Pitfall to avoid (RESEARCH.md Pitfall 2):** do not place this guard inside `Okozukai.Infrastructure/DependencyInjection.cs`'s `AddInfrastructure()` (`DependencyInjection.cs:10-16`) or any call that executes *after* `AddNpgsqlDbContext` — it must run before line 22, reading `builder.Configuration.GetConnectionString("okozukai")` directly.

---

### `src/Okozukai.ServiceDefaults/Extensions.cs` — health endpoint gate removal (D-09)

**Analog:** the file itself, current lines 109-126 (read in full this session).

**Current shape (lines 109-126):**
```csharp
public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    // Adding health checks endpoints to applications in non-development environments has security implications.
    // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
    if (app.Environment.IsDevelopment())
    {
        // All health checks must pass for app to be considered ready to accept traffic after starting
        app.MapHealthChecks(HealthEndpointPath);

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });
    }

    return app;
}
```

**Target shape (per RESEARCH.md Code Examples):**
```csharp
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

**What to preserve:** remove the `if (app.Environment.IsDevelopment())` wrapper and its two-line security-warning comment (lines 111-112) along with the gate — the comment explains the *gate's* rationale and is no longer accurate once the gate is gone. Keep both `MapHealthChecks` calls and their inline comments verbatim, including the `Predicate = r => r.Tags.Contains("live")` lambda on `AlivenessEndpointPath` — do not touch `HealthEndpointPath`/`AlivenessEndpointPath` constants (lines 18-19) or `AddDefaultHealthChecks` (lines 100-107), both out of scope for this phase.

---

### `.env.example` (new file, no analog)

No `.env*` file exists anywhere in the repo to copy structure from. Use the shape RESEARCH.md specifies directly (D-16), matching the three env vars this phase's decisions name explicitly (D-08 URL/port, D-13 connection string key):
```bash
# Required for the API to start in Production. Copy to .env and fill in real values;
# .env itself is gitignored (already present in .gitignore's "Environment Secrets" block).
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__okozukai=Host=postgres;Port=5432;Database=okozukai;Username=okozukai;Password=changeme
```
Place at repo root, alongside `.gitignore` and `README.md` (sibling to `src/`), matching where `.gitignore` already lives.

---

## Shared Patterns

### `IsDevelopment()` gate removal — consistent shape across both touched files
**Source:** `src/Okozukai.Api/Program.cs:38` and `src/Okozukai.ServiceDefaults/Extensions.cs:113` both use the identical guard expression `app.Environment.IsDevelopment()` / `if (app.Environment.IsDevelopment())`.
**Apply to:** Both edits should be done by literally deleting the `if (...)` line and its closing brace, dedenting the body — not by inverting the condition or adding a new one. This preserves git-blame-friendly diffs and matches the "subtractive, not conditional" instruction in RESEARCH.md Pattern 3.

### Startup ordering discipline
**Source:** `src/Okozukai.Api/Program.cs` full top-to-bottom read.
**Apply to:** All `Program.cs` edits in this phase. Current statement order is: `builder.AddServiceDefaults()` (9) → service registrations (12-31) → `builder.Build()` (33) → `app.MapDefaultEndpoints()` (35) → migration/seed block (38-44) → `UseExceptionHandler`/`UseCors`/HTTPS-redirect (46-53) → `UseAuthorization`/`MapControllers` (55-57) → `app.Run()` (59). The fail-fast guard (D-14) is the only insertion; it goes before line 22 (`AddNpgsqlDbContext`). Migration relocation and HTTPS-redirect removal are edits-in-place — no other statement should move.

### Plain-exception, no-custom-type error convention
**Source:** `src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs:56` (`if (retries == 0) throw;`) and the throw in D-14's guard.
**Apply to:** The new fail-fast guard. The codebase has no custom `ConfigurationException` type and no validation-attribute framework in use for startup config — a plain `InvalidOperationException` with a descriptive message is the established idiom; do not introduce a new exception type for this one guard.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `.env.example` | config | file-I/O | No `.env*`-family file exists anywhere in the repo yet; first of its kind. Use RESEARCH.md's specified shape (D-16) verbatim, no codebase precedent to reconcile against. |

## Assumption Carried From Research

`.gitignore` already contains the required `.env` entries (`.gitignore:71-77`, added in commit `880a8eb`, predating this phase). RESEARCH.md's Assumption A2 / Open Question 1 flags CONTEXT.md's D-16 claim ("`.gitignore` currently has no `.env` entry") as stale. **Do not schedule a `.gitignore`-modification task** — verified this session by direct read (lines 71-77 shown above). If the planner wants an explicit traceability checkbox for "`.env` is gitignored," point it at the existing block rather than creating a redundant edit.

## Metadata

**Analog search scope:** `src/Okozukai.Api/`, `src/Okozukai.ServiceDefaults/`, `src/Okozukai.Infrastructure/Persistence/`, `src/Okozukai.Infrastructure/DependencyInjection.cs`, `src/Okozukai.Api/Middlewares/GlobalExceptionHandler.cs`, `.gitignore` — all read directly this session (no re-reads of overlapping ranges).
**Files scanned:** 6 (all touched or style-analog files; every file in scope is small enough for a single Read pass)
**Pattern extraction date:** 2026-08-21
