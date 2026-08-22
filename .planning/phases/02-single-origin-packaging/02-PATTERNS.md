# Phase 2: Single-Origin Packaging - Pattern Map

**Mapped:** 2026-08-21
**Files analyzed:** 8 (5 modified, 3 created)
**Analogs found:** 5 exact self-analog (modify-in-place) / 3 no-analog (net-new infra files)

Note: this phase is almost entirely edits to existing files (their own prior content is the
"analog") plus three genuinely new repo-root files with no prior analog. There is no sibling
Dockerfile/compose file elsewhere in the repo to copy from.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `src/Okozukai.Api/Program.cs` | config/bootstrap (middleware pipeline) | request-response | itself (current pipeline, quoted below) | exact — in-place edit |
| `src/Okozukai.Frontend/src/api/client.ts` | service (HTTP client config) | request-response | itself + `journalService.ts`/`transactionService.ts` (already-relative call sites) | exact — in-place edit |
| `src/Okozukai.Frontend/vite.config.ts` | config | request-response (dev proxy) | itself (current `server` block) | exact — in-place edit |
| `src/Okozukai.AppHost/Program.cs` | provider/orchestration (Aspire dev-time) | event-driven (service discovery) | itself (current `frontend`/`api` wiring) | exact — in-place edit |
| `.env.example` | config | — | itself (Phase 1 D-16 content, see below — could not `Read` directly, permission-denied on `.env*` paths; content reconstructed from `01-RESEARCH.md` citations) | exact — in-place edit |
| `Dockerfile` (repo root) | config/build | file-I/O (build stage artifacts) | **none in repo** | no analog |
| `compose.yaml` (repo root) | config/orchestration | event-driven (container lifecycle) | **none in repo** | no analog |
| `.dockerignore` (repo root) | config | — | **none in repo** | no analog |

## Pattern Assignments

### `src/Okozukai.Api/Program.cs` (bootstrap/config, request-response)

**Analog:** itself — current full pipeline, read in full (66 lines), quoted verbatim below with line numbers so the planner can specify exact insertion points.

```csharp
// lines 1-5 — imports
using Microsoft.EntityFrameworkCore;
using Okozukai.Api.Middlewares;
using Okozukai.Application;
using Okozukai.Infrastructure;
using Okozukai.Infrastructure.Persistence;

// line 7
var builder = WebApplication.CreateBuilder(args);

// line 9
builder.AddServiceDefaults();

// lines 11-21 — CORS block to DELETE per D-07 (AddCors + AddDefaultPolicy)
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// lines 23-29 — Phase 1 D-14 fail-fast guard, UNCHANGED, do not touch
var connectionString = builder.Configuration.GetConnectionString("okozukai");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings__okozukai. " +
        "Set it as an environment variable before starting the API.");
}

// lines 31-40 — unchanged
builder.AddNpgsqlDbContext<OkozukaiDbContext>("okozukai");
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddOpenApi();

// line 42
var app = builder.Build();

// line 44
app.MapDefaultEndpoints();

// line 47 — Phase 1 migration call, unchanged
app.Services.ApplyDatabaseMigrations();

// lines 49-54 — dev-only seed + OpenAPI, unchanged
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("--> Environment is Development. Seeding data...");
    app.Services.SeedDevelopmentData();
    app.MapOpenApi();
}

// line 56 — unchanged
app.UseExceptionHandler();

// line 58 — DELETE per D-07 (paired with the AddCors block above)
app.UseCors();

// line 60 — unchanged
app.UseAuthorization();

// line 62 — INSERTION POINT: UseStaticFiles goes BEFORE this per D-01's ordering
// ("UseStaticFiles() ... MapControllers() ... MapFallbackToFile() after MapControllers")
// — actual required order per D-01 text and RESEARCH.md Pattern 2:
//   UseStaticFiles (with OnPrepareResponse) -> UseAuthorization -> MapControllers -> MapFallbackToFile
// so UseStaticFiles must land BEFORE line 60's UseAuthorization, not between 60 and 62.
app.MapControllers();

// NEW: app.MapFallbackToFile("index.html");  <-- add after MapControllers, per D-01/Pitfall 3

// line 64
app.Run();

// line 66 — DO NOT REMOVE — required by WebApplicationFactory<TProgram> (used by all 22
// integration tests via CustomWebApplicationFactory<TProgram>)
public partial class Program { }
```

**Critical constraint (from RESEARCH.md Pitfall 1, verified against `CustomWebApplicationFactory.cs` below):** do not call `builder.WebHost.UseWebRoot(...)` or set a custom `WebRootPath` anywhere in this file. Let the default `wwwroot` convention resolve — populated in the Docker image, absent (safely) during `dotnet test`.

---

### `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs` (test harness — read for constraint verification only, NOT modified this phase)

Full file confirmed 51 lines. No `UseWebRoot`/`WebRootPath` override anywhere — confirms Pitfall 1's safety argument holds today:

```csharp
// lines 12-24
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    static CustomWebApplicationFactory()
    {
        // Program.cs's fail-fast connection-string guard (D-14) runs as a top-level
        // statement, before this factory's ConfigureWebHost override ever executes, so
        // it cannot see the in-memory DbContext swap below. Supply a placeholder value
        // so the guard passes; it is never actually dialed, since ConfigureServices
        // replaces OkozukaiDbContext with an in-memory provider immediately afterward.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__okozukai",
            "Host=localhost;Port=5432;Database=okozukai_test_placeholder;Username=test;Password=test");
    }

    // lines 26-50
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // removes all EF Core registrations, swaps in UseInMemoryDatabase
            // ... (unrelated to static files)
        });

        builder.UseEnvironment("Development");
    }
}
```

**Confirmed:** no `WebRootPath`/`UseWebRoot` call anywhere in the file. This is the concrete evidence the planner needs to assert the "safe with no wwwroot" claim rather than take it on faith. `dotnet test Okozukai.slnx --no-build -nologo` (22/22) is the verification gate for the `Program.cs` edit, and it must be run before Dockerfile work, per RESEARCH.md Pitfall 1.

---

### `src/Okozukai.Frontend/src/api/client.ts` (service, request-response) — full file, 20 lines

**Analog:** itself.

```typescript
import axios from 'axios';

const apiClient = axios.create({
    baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5005',   // line 4 — D-05 target
    headers: {
        'Content-Type': 'application/json'
    },
    timeout: 10000,
    paramsSerializer: (params) => {
        const sp = new URLSearchParams();
        for (const [key, val] of Object.entries(params)) {
            if (Array.isArray(val)) val.forEach(v => sp.append(key, String(v)));
            else if (val !== undefined && val !== null) sp.append(key, String(val));
        }
        return sp.toString();
    }
});

export default apiClient;
```

Per D-05: line 4 becomes `baseURL: ''` (or the key is dropped entirely — axios defaults to relative when `baseURL` is falsy) — no `VITE_API_URL` read, no localhost fallback.

**Proof no service file needs editing (D-05's "by construction" claim):** both call sites already send full `/api/...` paths through the shared `apiClient`:
- `journalService.ts:6` — `apiClient.get<JournalResponse[]>('/api/journals')`
- `journalService.ts:16` — `apiClient.post<JournalResponse>('/api/journals', request)`
- `transactionService.ts:19,29,43,48,53` — `'/api/transactions'`, `'/api/transactions/summary'`, `'/api/transactions/grouped'`, `'/api/transactions/spending-by-tag'`, `'/api/transactions/spending-by-tag-monthly'`
- `transactionService.ts:63,68` — `'/api/tags'`

Every path is already relative to `apiClient`'s `baseURL`; once `baseURL` is empty, `axios` resolves these against the page's own origin. No further files need touching for ORIG-01.

---

### `src/Okozukai.Frontend/vite.config.ts` (config, request-response dev proxy) — full file, 41 lines

**Analog:** itself — current `server` block (lines 31-35) is the insertion point.

```typescript
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    vue(),
    VitePWA({
      registerType: 'autoUpdate',
      manifest: { /* name, short_name, description, theme_color, icons */ }
    })
  ],
  server: {                                    // lines 31-35 — D-06 proxy goes here
    port: process.env.PORT ? parseInt(process.env.PORT) : 5173,
    strictPort: true,
    host: true
    // NEW: proxy: { '/api': { target: process.env.services__api__http__0 ?? 'http://localhost:5005', changeOrigin: true } }
  },
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/tests/**/*.spec.ts']
  }
})
```

**Note (MEDIUM confidence per RESEARCH.md Pattern 4):** the exact Aspire-injected env var name for `AddViteApp`-wrapped resources (`services__api__http__0`) was not verified verbatim against `AddViteApp` specifically — RESEARCH.md flags this as a plan-time verification point (`console.log(process.env)` inside `vite.config.ts` once under `aspire run`). The `?? 'http://localhost:5005'` fallback in the proxy target covers the dev case regardless.

---

### `src/Okozukai.AppHost/Program.cs` (orchestration, event-driven service discovery) — full file, 38 lines

**Analog:** itself.

```csharp
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddConnectionString("okozukai");

var tailnetIp = builder.Configuration["TAILNET_IP"];
var apiPort = int.Parse(builder.Configuration["TAILNET_API_PORT"] ?? "5005");     // line 8 — TAILNET_API_PORT read, D-06 removes
var frontendPort = int.Parse(builder.Configuration["TAILNET_FRONTEND_PORT"] ?? "5173");

var api = builder.AddProject<Projects.Okozukai_Api>("api")
    .WithReference(db)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

if (!string.IsNullOrEmpty(tailnetIp))
{
    var aspnetUrls = "http://0.0.0.0:" + apiPort;
    api.WithEndpoint("http", e => { e.Port = apiPort; e.IsProxied = false; })
       .WithEnvironment("ASPNETCORE_URLS", aspnetUrls);
}

// AddViteApp registers the http endpoint and PORT env var itself —
// calling WithHttpEndpoint here would create a duplicate endpoint.
var frontend = builder.AddViteApp("frontend", "../Okozukai.Frontend")
    .WithReference(api)                              // line 25 — already unconditional; this is
                                                       // what injects services__api__http__0
    .WithExternalHttpEndpoints();

if (!string.IsNullOrEmpty(tailnetIp))
{
    frontend.WithEndpoint("http", e => { e.Port = frontendPort; e.IsProxied = false; });

    var tailnetApiUrl = "http://" + tailnetIp + ":" + apiPort;
    frontend.WithEnvironment("VITE_API_URL", tailnetApiUrl);      // line 33 — DELETE per D-06
}
else
    frontend.WithEnvironment("VITE_API_URL", api.GetEndpoint("http"));  // line 36 — DELETE per D-06

builder.Build().Run();
```

Per D-06: delete lines 33 and 36 (both `VITE_API_URL` injections). `apiPort`/`TAILNET_API_PORT` (line 8) is still used by the `tailnetIp` branch's `ASPNETCORE_URLS` wiring for the API itself — **do not delete `apiPort`**, only the two `frontend.WithEnvironment("VITE_API_URL", ...)` calls it fed into. `frontend.WithReference(api)` (line 25) already exists and is untouched — it is the mechanism D-06 relies on for `services__api__http__0` injection into the Vite dev-server process.

---

### `.env.example` (repo root, config) — Phase 1 D-16 content

**Could not `Read` directly this session** — the tool denied access to `.env.example` under this session's permission settings (directory-level deny rule matching `.env*` paths), and `Bash(cat ...)`/`Bash(grep ...)` against the same path were denied for the same reason. Content is reconstructed from `01-RESEARCH.md`'s own verified citations of it (lines 33-34, 183): it documents three variables — `ConnectionStrings__okozukai`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS` — with `.env` gitignored and `.env.example` committed.

**Planner action:** extend, don't replace. Per RESEARCH.md Pattern 3 and Pitfall 4, this phase's edit adds `POSTGRES_PASSWORD` (must match the `Password=` segment of `ConnectionStrings__okozukai`, quoted if it contains `;`/`=` per Pitfall 5) and, if parameterized, `POSTGRES_USER`/`POSTGRES_DB` (recommend hardcoding `okozukai` for both in `compose.yaml` instead, per Pitfall 4's "match exactly" requirement — simpler than adding more `.env` surface). **The plan/executor should `Read` this file directly at execution time** (a coding agent's permission context differs from this pattern-mapping agent's) rather than rely on this reconstruction.

---

### `Dockerfile`, `compose.yaml`, `.dockerignore` (repo root) — NO ANALOG

**Confirmed: no Dockerfile, compose file, or `.dockerignore` exists anywhere in this repo** (RESEARCH.md verified this via `find` this session, and no additional file matching these names was found during this pass either). Do not fabricate a nearest-analog from an unrelated project — there is none in this codebase to copy structure from.

Instead, the planner should build these three files directly from the following concrete inputs, all confirmed this session:

**Version facts for base-image tags:**
- `.csproj` `TargetFramework`: **`net10.0`** for all six projects (`Okozukai.Api`, `Okozukai.AppHost`, `Okozukai.Application`, `Okozukai.Domain`, `Okozukai.Infrastructure`, `Okozukai.ServiceDefaults`) — confirmed via grep this session.
- SDK pin: **absent** — no `global.json` found anywhere in the repo.
- Node version pin: **absent** — `src/Okozukai.Frontend/package.json` has no `"engines"` key; no `.nvmrc` found. RESEARCH.md's own session found local Node `v22.18.0` and recommends `node:22-slim`/`node:22-alpine` for the build stage given Vite 7's 20.19+/22.12+ floor.

**Project reference graph the Dockerfile's `dotnet restore`/`COPY` steps must mirror** (`Okozukai.Api.csproj` `<ProjectReference>` entries, lines 19-22): `Okozukai.Application`, `Okozukai.Domain`, `Okozukai.Infrastructure`, `Okozukai.ServiceDefaults`. `Okozukai.AppHost` and the empty `Okozukai.Web` leftover are not referenced and must not be copied into the build context for the API's publish stage.

**Health contract these files must consume (Phase 1 D-11/D-12, unchanged by this phase per D-09):**
- `/health` — readiness, includes the Aspire-registered PostgreSQL check (`AddNpgsqlDbContext`, already wired — do not add a health-check package).
- `/alive` — liveness, process-only `self` check. **Do not use `/alive` for `depends_on`/`HEALTHCHECK` gating** — it will report healthy before Postgres is reachable (RESEARCH.md Anti-Patterns / Pitfall list).

**Env var the app container needs at runtime (Phase 1 D-13, unchanged):** `ConnectionStrings__okozukai` — read automatically by ASP.NET Core's built-in `__`-to-`:` config provider, no code involved; supplied via compose `env_file: .env`.

**Built `dist/` layout the Dockerfile's final `COPY --from=frontend-build` must expect** (verified `npm run build` output this session, per RESEARCH.md Code Examples):
```
dist/index.html                    # unhashed — no-cache
dist/manifest.webmanifest          # unhashed — no-cache
dist/registerSW.js                 # unhashed — no-cache
dist/sw.js                         # unhashed — no-cache
dist/workbox-<hash>.js             # hashed
dist/vite.svg                      # static, unhashed
dist/assets/*.css                  # content-hashed — safe to cache 1yr immutable
dist/assets/*.js                   # content-hashed — safe to cache 1yr immutable
```
This structural split (`/assets/*` always hashed, everything at `dist/` root never hashed) is what the `OnPrepareResponse` cache-header branch in `Program.cs` keys off — see RESEARCH.md Pattern 2 for the C# code.

**Compose service count and port-publishing constraint (D-03):** exactly two services (`db`, `app`); only `app` publishes a port to the host — no third (reverse-proxy) service, per D-01's explicit reversal (do not re-propose).

**Volume mount path (Pitfall 2, the single most dangerous silent-failure mode in this phase):** the named volume must mount at exactly `/var/lib/postgresql/data`, not the parent `/var/lib/postgresql` — `postgres:17`'s fixed `PGDATA` path.

## Shared Patterns

### No-`wwwroot`-override constraint
**Source:** `src/Okozukai.Api/Program.cs` (current file has no `WebRootPath`/`UseWebRoot` call) + `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs` (no override either)
**Apply to:** the `Program.cs` edit only file in this phase's backend scope.
**Rule:** do not introduce `builder.WebHost.UseWebRoot(...)` or `WebApplicationOptions.WebRootPath` anywhere — this is what keeps the 22 integration tests passing with no `wwwroot` on disk.

### Relative-path API calls
**Source:** `src/Okozukai.Frontend/src/api/journalService.ts`, `transactionService.ts` (unmodified this phase, cited as proof)
**Apply to:** `client.ts` only — the service files are the evidence the `baseURL` change is sufficient by itself.

### E2E base-URL wiring (D-06 risk check)
**Source:** `src/Okozukai.Frontend/playwright.config.ts` (21 lines, full file read) + `src/Okozukai.Frontend/e2e/dashboard.spec.ts` (only e2e spec file in the repo — confirmed via `find`)
```typescript
// playwright.config.ts:11
use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5173',
    ...
}
```
```typescript
// dashboard.spec.ts:3, 7, 139
const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5173';
...
await page.goto(BASE_URL);
```
**Finding:** neither file references `VITE_API_URL` or any API port directly — both only navigate to the frontend dev-server origin (`5173`) via `BASE_URL`/Playwright's own `baseURL`, and rely on the *browser* making relative `/api/...` calls once loaded, exactly like production. **D-06's dev-proxy change (the `vite.config.ts` `server.proxy` addition) is what keeps these 14 tests* passing** — no E2E test file itself needs editing. (*RESEARCH.md's stated count of 14 Playwright tests refers to individual `test(...)` blocks inside this one spec file, not 14 separate files — only one e2e spec file exists in the repo.)

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Dockerfile` (repo root) | config/build | file-I/O | No Dockerfile exists anywhere in the repo — first one this phase creates. See concrete inputs list above (version facts, project-reference graph, `dist/` layout). |
| `compose.yaml` (repo root) | config/orchestration | event-driven | No compose file exists anywhere in the repo. See concrete inputs list above (health contract, env vars, volume path, D-03's two-service constraint). |
| `.dockerignore` (repo root) | config | — | No `.dockerignore` exists. RESEARCH.md's Code Examples section already has a drafted version (`**/bin/`, `**/obj/`, `**/node_modules/`, `**/dist/`, `.git/`, `.planning/`, `tests/`, `.env`/`.env.*` with `!.env.example` exception) — treat that as the starting draft, not an "analog" in the codebase sense. |

## Metadata

**Analog search scope:** `src/Okozukai.Api/`, `src/Okozukai.Frontend/src/api/`, `src/Okozukai.Frontend/` (config files), `src/Okozukai.AppHost/`, `tests/Okozukai.IntegrationTests/`, `src/Okozukai.Frontend/e2e/`, repo root.
**Files scanned:** `Program.cs` (API, full 66 lines), `CustomWebApplicationFactory.cs` (full 51 lines), `client.ts` (full 20 lines), `journalService.ts`/`transactionService.ts` (grepped for import + `/api/` call sites), `vite.config.ts` (full 41 lines), `AppHost/Program.cs` (full 38 lines), `playwright.config.ts` (full 21 lines), `e2e/dashboard.spec.ts` (grepped for baseURL/localhost/process.env), all six `.csproj` files (`TargetFramework` grep), `package.json` (`engines` grep — absent), `.env.example` (access denied this session — reconstructed from `01-RESEARCH.md` citations).
**Pattern extraction date:** 2026-08-21
