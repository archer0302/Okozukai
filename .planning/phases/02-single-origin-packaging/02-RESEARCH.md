# Phase 2: Single-Origin Packaging - Research

**Researched:** 2026-08-21
**Domain:** Combined .NET+Vue Docker packaging, ASP.NET Core static-file/SPA-fallback serving, PostgreSQL container provisioning, Aspire dev-time service discovery
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Single-Origin Entry Point (ORIG-03, PKG-01, PKG-02)**
- **D-01:** The app runs as one container. The API image serves the built Vue SPA itself — `app.UseStaticFiles()` plus `app.MapFallbackToFile("index.html")` after `app.MapControllers()` in `src/Okozukai.Api/Program.cs`. There is no nginx or Caddy container and no reverse-proxy config file anywhere in the repo. Routing needs no rewriting: every controller is already `[Route("api/[controller]")]` and every frontend service call already sends a full `/api/...` path, so `/api/*` hits controllers and everything else falls through to the SPA. Reversibility: costly. Accepted cost: a frontend-only change rebuilds the .NET image.
- **D-02:** PostgreSQL stays in its own container — the official `postgres:17` image with a named volume (PKG-04). Bundling the database into the app image was considered and rejected.
- **D-03:** Compose has exactly two services — `db` and the app. Only the app service publishes a port to the host.
- **D-04:** Cache headers (PKG-06) are C# `StaticFileOptions.OnPrepareResponse`, not proxy config — a direct consequence of D-01. Roughly: long immutable caching for Vite's content-hashed assets, no-cache for `index.html` and the service worker.

**API Base URL (ORIG-01, ORIG-02)**
- **D-05:** `VITE_API_URL` is deleted entirely. `src/Okozukai.Frontend/src/api/client.ts:4` currently reads `baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5005'`; the env var and the localhost fallback both go away and `baseURL` becomes empty/relative.
- **D-06:** Consequence of D-05 — `aspire run` needs a Vite dev-server proxy. `vite.config.ts` gains a `server.proxy` entry forwarding `/api` to the API. The `VITE_API_URL` / `TAILNET_API_PORT` injection in `src/Okozukai.AppHost/Program.cs` comes out. Not discussed in detail; the planner works out the proxy shape. Check the 14 Playwright E2E tests — they may carry base-URL assumptions.
- **D-07:** CORS is removed outright — delete the `AddCors`/`AddDefaultPolicy` block at `src/Okozukai.Api/Program.cs:13-21` and the `app.UseCors()` call at line 58.

**Health Endpoint Exposure**
- **D-08:** `/health` and `/alive` become reachable from the Tailnet, and that is accepted. Do not add `RequireHost`, auth, or a management port to compensate.
- **D-09:** The Phase 1 health contract (D-12) is unchanged and is what compose consumes: `/health` = ready (includes the PostgreSQL check), `/alive` = live (process-only `self` check). Do not add health-check packages or DI wiring.

### Claude's Discretion

The user chose to lock the entry-point shape and leave the rest to the planner, on the grounds that these are all reversible if wrong:

- Dockerfile stage layout — node stage building `dist/`, dotnet stage publishing the API, final image copying both. Base images, non-root user, and whether to use `dotnet publish /t:PublishContainer` instead of a hand-written Dockerfile.
- Where the built SPA lands in the image (`wwwroot` is the obvious choice) and exact middleware ordering around `UseStaticFiles` / `MapFallbackToFile`.
- Exact cache-header values and durations for D-04.
- Compose file details: named volume name, `env_file` wiring, restart policy, `depends_on` conditions.
- The `vite.config.ts` dev-proxy shape (D-06).
- Whether the app container gets its own `HEALTHCHECK`, and behaviour when the API is down (maintenance page etc.) — explicitly called low-stakes.

### Deferred Ideas (OUT OF SCOPE)

- **Everything in one container, Postgres included** — raised and rejected in favour of D-02. Not a future phase item; recorded so it is not re-proposed.
- **`RequireHost` / auth / management port for health endpoints** — declined under D-08.
- **Volume/data hazards not discussed in detail** — the user chose to leave these to the planner (this research resolves the specific questions raised).
- **Caching & PWA update behaviour beyond D-04** — whether `registerType` stays `autoUpdate` or becomes a prompt was offered and not selected; `autoUpdate` is the current setting and the simpler default.
- **GitHub Actions deployment workflow** — CI/CD is Out of Scope for milestone 1. Forward constraint: the homelab box is a self-hosted runner, so a future workflow builds and runs in place with no registry hop. **This phase's packaging must keep local build-and-run viable — no `image:` pull-only compose service, `build:` context stays in the repo.**

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| ORIG-01 | Frontend calls the API on a relative path | `client.ts:4`'s `baseURL` is the only place an absolute URL can enter the bundle (verified — every service file already sends `/api/...` paths). See Code Examples, Common Pitfalls. |
| ORIG-02 | API no longer enables permissive CORS | `Program.cs:13-21,58` is the sole CORS code in the API — verified no controller-level `[EnableCors]` attributes exist. See Architecture Patterns. |
| ORIG-03 | Single origin serves both frontend and API | D-01's `UseStaticFiles` + `MapControllers` + `MapFallbackToFile` ordering, verified safe against the empty-`wwwroot` test-boot case. See Common Pitfalls Pitfall 1 (the phase's highest-risk item). |
| PKG-01 | API builds into a container image via repeatable multi-stage build | Dockerfile stage layout research below — Node stage + .NET stage + runtime stage, with `dotnet publish /t:PublishContainer` ruled out because it cannot execute a Node build step. See Architecture Patterns, Code Examples. |
| PKG-02 | Frontend builds to production static assets | `npm run build` output verified this session — exact `dist/` file list and hashing scheme documented in Code Examples. |
| PKG-03 | Single command brings up the whole stack | `compose.yaml` shape — two services, `depends_on: condition: service_healthy`, `env_file`. See Architecture Patterns. |
| PKG-04 | PostgreSQL data survives container recreation | Named-volume mount path (`/var/lib/postgresql/data`), `POSTGRES_DB` first-boot behavior, and the interaction with Phase 1's `3D000`/`CREATEDB` fallback — see Common Pitfalls Pitfall 4, the phase's highest-stakes item per CONTEXT.md. |
| PKG-05 | Secrets supplied without being committed | `.env` (gitignored, Phase 1 D-16) via compose `env_file:` — no new mechanism needed. See Architecture Patterns. |
| PKG-06 | Cache headers let the PWA service worker pick up new deployments | `StaticFileOptions.OnPrepareResponse` per-path Cache-Control values, verified against the actual `vite-plugin-pwa` 1.2.0 build output. See Code Examples. |

</phase_requirements>

## Summary

This phase's engineering is concentrated in two places: getting a Node build stage and a .NET publish stage into one Dockerfile without either tool stepping on the other's cache, and getting `UseStaticFiles`/`MapFallbackToFile` to coexist safely with the 22 `WebApplicationFactory`-booted integration tests that will never see a `wwwroot` folder. Both are solved problems with a documented, verifiable safe path: ASP.NET Core's static-file middleware degrades gracefully (no exception, just an empty/404 file provider) when the default `wwwroot` convention path doesn't exist on disk — confirmed via the framework's own GitHub issue tracker — **as long as `Program.cs` never explicitly overrides `WebRootPath`/calls `UseWebRoot()` to a custom path**. A second, unrelated GitHub issue (#48620) about `DirectoryNotFoundException` at startup is specifically about apps that *do* call `UseWebRoot()` to a nonexistent custom directory; this phase must avoid that pattern and simply let the default `wwwroot` convention resolve, which is what makes both the container (where `wwwroot` physically exists) and the test run (where it doesn't) safe with the same code.

The second concentration of risk is data durability (PKG-04), which CONTEXT.md flags as the highest-stakes item in the phase. Verified against the official `postgres` Docker Hub image docs: `POSTGRES_DB` creates the named database on first boot of an *empty* data directory — this makes Phase 1's `3D000`/`CREATEDB` auto-create fallback moot on a fresh volume, because the fallback only fires if the database is missing, and it never will be if `POSTGRES_DB` names it correctly. `POSTGRES_USER` (when set to something other than the default `postgres`) is created with superuser privileges, so `CREATEDB` is available either way. The volume must mount at exactly `/var/lib/postgresql/data`, not the parent `/var/lib/postgresql` — mounting the parent silently fails to persist data across recreation, a well-documented image-specific gotcha. `postgres:17` uses this fixed path; version-specific `PGDATA` paths (`/var/lib/postgresql/18/docker`) are an 18+ change and do not apply to the locked `postgres:17` image.

Third, `VITE_API_URL`'s removal (D-05/D-06) does not require new AppHost plumbing: `frontend.WithReference(api)` already exists in `src/Okozukai.AppHost/Program.cs:25` (unconditionally, unrelated to the `tailnetIp` branch), and .NET Aspire's service-discovery convention — confirmed via the official docs, which explicitly demonstrate it working from a Node/TypeScript process reading `process.env` — injects `services__api__http__0` into any referencing resource's environment regardless of language. `vite.config.ts`'s `server.proxy` can read that variable directly with no AppHost code addition beyond deleting the two `VITE_API_URL`/`TAILNET_API_PORT` lines D-06 already calls out.

**Primary recommendation:** Hand-written multi-stage Dockerfile (Node build stage → .NET SDK publish stage → `aspnet:10.0` runtime stage copying both outputs into `wwwroot`), classic `UseStaticFiles()` (not the .NET 9+ manifest-driven `MapStaticAssets()`, which would silently fail to serve SPA files copied into `wwwroot` post-publish), `OnPrepareResponse` cache-header branching on path prefix, and a two-service `compose.yaml` with the named volume mounted at `/var/lib/postgresql/data` and `depends_on: condition: service_healthy` gated on the API's own `HEALTHCHECK` hitting `/health`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Single-origin request routing (`/api/*` vs SPA fallback) | API / Backend | Browser / Client | ASP.NET Core's endpoint routing decides `/api/*` vs. everything-else inside the API process (D-01); the browser only ever sees one origin |
| Static asset serving + cache headers | API / Backend | CDN / Static (absent this milestone) | `StaticFileOptions.OnPrepareResponse` in C# per D-04 — there is no CDN/edge tier in this topology, so the API process is the only place cache policy can be set |
| SPA build output | Browser / Client | API / Backend | Vite produces the artifact; the API tier only serves the already-built files, does no server-side rendering |
| Container orchestration / startup ordering | API / Backend (compose) | Database / Storage | `compose.yaml`'s `depends_on: condition: service_healthy` lives at the orchestration layer, one level above both containers, but is authored alongside the app |
| Database provisioning + persistence | Database / Storage | — | `postgres:17` container + named volume; entirely self-contained, no app-tier code involved (D-02) |
| Dev-time service discovery (Vite proxy target) | API / Backend (AppHost, dev-only) | Browser / Client (dev server) | Aspire's `WithReference` injects env vars into the Vite dev-server *process* (Node), not into the browser bundle — this is a build/dev-time value, never shipped to the client |

## Package Legitimacy Audit

**Not applicable this phase.** No new NuGet or npm packages are installed. This phase's work is entirely: a Dockerfile (new base images, not packages — `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, `node:22-slim`, `postgres:17`, all official first-party Microsoft/Docker/PostgreSQL images, not subject to the npm/PyPI slopsquatting vector this gate targets), a `compose.yaml`, a `.dockerignore`, and edits to existing `Program.cs`/`client.ts`/`vite.config.ts` files. If a later plan step considers a `.dockerignore` linter or a compose-validation package, run the Package Legitimacy Gate at that time.

## Standard Stack

### Core (existing — no new installs)
| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|---------------|
| .NET SDK / ASP.NET Core | `net10.0` [VERIFIED: src/Okozukai.Api/Okozukai.Api.csproj:4 — `<TargetFramework>net10.0</TargetFramework>`], local toolchain `10.0.100` [VERIFIED: `dotnet --version` output this session] | Backend build target | Already the repo's pinned TFM; no `global.json` pins the SDK feature band, so the container's SDK image tag should match `10.0` |
| Node.js | No pin file in repo (`global.json`/`.nvmrc`/`package.json engines` all absent — [VERIFIED: `find` for these files this session returned none, and `package.json` has no `engines` key]); locally installed `v22.18.0` [VERIFIED: `node --version` this session]; `.planning/codebase/STACK.md` documents "Node.js 20+" [CITED: .planning/codebase/STACK.md, dated 2026-08-19 — predates this session] | Frontend build tooling | Vite 7.3.1 (the pinned version, `^7.3.1` in `package.json` [VERIFIED: src/Okozukai.Frontend/package.json:32]) requires Node **20.19+ or 22.12+** [CITED: vite.dev — Vite 7 release notes/migration guide, cross-checked via WebSearch] — so the STACK.md's "20+" is imprecise; a bare Node 20.0–20.18 would not actually run this Vite version. **Recommend `node:22-slim` or `node:22-alpine` for the Docker build stage** (matches the locally installed major version and clears the 22.12+ floor without needing the narrower 20.19+ patch check). |
| Docker (build/runtime host) | `29.6.1` [VERIFIED: `docker --version` this session]; compose plugin present at `~/.docker/cli-plugins/docker-compose` [VERIFIED: `ls` this session] but the Docker **daemon is not running** in this research sandbox (`docker info` fails to connect to the socket) | Container build/run | See Environment Availability — this affects only where the verification `docker compose up` step can actually execute, not the compose file's correctness |
| `postgres` (Docker Hub official image) | `17` [locked, D-02] | Database container | Official image; `PGDATA` at `/var/lib/postgresql/data` is fixed for this major version (version-specific `PGDATA` paths are an 18+ change) [CITED: hub.docker.com/_/postgres] |
| `Npgsql` / `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.0` [VERIFIED: src/Okozukai.Infrastructure/Okozukai.Infrastructure.csproj — confirmed in Phase 1 RESEARCH.md's own `.csproj` read, unchanged this phase] | ADO.NET / EF Core PostgreSQL driver | Connection-string quoting rules (below) are this library's, not raw libpq's |
| `vite-plugin-pwa` | `1.2.0` [VERIFIED: src/Okozukai.Frontend/node_modules/vite-plugin-pwa/package.json — installed version matches the `^1.2.0` pin in package.json], `registerType: 'autoUpdate'` [VERIFIED: src/Okozukai.Frontend/vite.config.ts:10] | Service-worker generation | Already configured; this phase serves its output with correct cache headers, does not touch its config |

### Base Images for Dockerfile (recommendation)
| Stage | Image | Purpose |
|-------|-------|---------|
| `frontend-build` | `node:22-slim` | `npm ci && npm run build` → produces `dist/` |
| `backend-build` | `mcr.microsoft.com/dotnet/sdk:10.0` | `dotnet restore` + `dotnet publish -c Release` for `Okozukai.Api` |
| `final` | `mcr.microsoft.com/dotnet/aspnet:10.0` | Runtime — copies published API output + `dist/` (into `wwwroot`) from the two build stages; no SDK, no Node in the final image |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-written multi-stage Dockerfile | `dotnet publish /t:PublishContainer` (.NET SDK container-building tools, no Dockerfile) | **Ruled out.** `PublishContainer` cannot execute `RUN` commands or a non-.NET build step — there is no way to interleave `npm run build` into its image-build sequence. A Dockerfile is required the moment a non-.NET build stage (Node) must run before the final `COPY`. [CITED: codewithmukesh.com/milanjovanovic.tech, cross-checked against the .NET container-tools documentation's own stated limitation — "if you need... multi-stage builds with non-.NET components... you still need a Dockerfile"] |
| `node:22-slim` build stage | `node:22-alpine` | Alpine is smaller but uses musl libc, which occasionally trips up native npm deps (none currently in this frontend's dependency tree, so either works); `-slim` (Debian) is the safer default when uncertain, `-alpine` is a valid discretionary swap if image size matters more |
| `UseStaticFiles()` (classic, disk-scanning) | `MapStaticAssets()` (.NET 9+ manifest-driven) | **Do not use for the SPA's `wwwroot` content.** `MapStaticAssets` serves only files present in a build-time manifest generated during the *API project's own* `dotnet publish`; files copied into `wwwroot` by a later Docker `COPY` step (the frontend's `dist/`) are not in that manifest and will not be served. [CITED: learn.microsoft.com/aspnet/core/fundamentals/static-files — "Files aren't part of the manifest when they're: Located outside the build-time web root... To serve files that aren't in the manifest, call UseStaticFiles"] — this is a documented framework behavior, not a workaround. |

**Installation:** No new package installs. Base image pulls only (`docker pull` happens implicitly on `docker build`).

## Architecture Patterns

### System Architecture Diagram

```
                    Browser (single origin, e.g. http://<tailnet-host>:8080)
                                       │
                     GET /            GET /api/transactions          GET /assets/index-x7TlS8Fk.js
                        │                     │                                │
                        ▼                     ▼                                ▼
        ┌───────────────────────────────────────────────────────────────────────────┐
        │                    ASP.NET Core Kestrel (single app container)             │
        │                                                                             │
        │   UseExceptionHandler → UseStaticFiles (Cache-Control via                  │
        │   OnPrepareResponse, path-branched) → UseAuthorization →                   │
        │   MapControllers ([Route("api/[controller]")], matches /api/* first) →     │
        │   MapFallbackToFile("index.html")  (order = int.MaxValue, always last)     │
        │                                                                             │
        │   /assets/*.js|css → served from wwwroot/assets, immutable 1yr cache        │
        │   /index.html, /sw.js, /registerSW.js, /manifest.webmanifest → no-cache     │
        │   /api/*  → routed to TransactionsController / JournalsController /         │
        │             TagsController → Application services → EF Core                │
        │   anything else (no file extension, e.g. /dashboard) → index.html           │
        │             (client-side Vue Router takes over)                             │
        └───────────────────────────┬─────────────────────────────────────────────────┘
                                     │ ConnectionStrings__okozukai (env var, from .env
                                     │ via compose env_file, PKG-05)
                                     ▼
                    ┌─────────────────────────────────┐
                    │  postgres:17 container (db)      │
                    │  named volume → /var/lib/         │
                    │  postgresql/data (PKG-04)         │
                    │  POSTGRES_DB creates schema DB     │
                    │  on first boot of empty volume     │
                    └─────────────────────────────────┘

        depends_on: db: condition: service_healthy   (compose-level ordering,
        app's own HEALTHCHECK polls GET /health — includes the Postgres check)
```

A reader can trace the primary use case top-to-bottom: one browser origin → Kestrel's request pipeline branches on path shape (API route vs. static file vs. SPA fallback) → API calls flow into the existing Application/Infrastructure layers → the only cross-container hop is the API's own Postgres connection, gated at startup by `compose`'s health-check ordering.

### Recommended Project Structure
```
/ (repo root)
├── Dockerfile                 # new — multi-stage: node build, dotnet publish, aspnet runtime
├── compose.yaml                # new — db + app, two services
├── .dockerignore                # new — bin/, obj/, node_modules/, dist/, .planning/, .git/, tests/
├── .env.example                 # exists (Phase 1, D-16) — compose env_file source, gitignored .env
└── src/
    ├── Okozukai.Api/
    │   ├── Program.cs           # D-01, D-04, D-07 land here
    │   └── wwwroot/              # does NOT exist in the git repo; created only inside the
    │                              # Docker image by COPY --from=frontend-build /app/dist ./wwwroot
    └── Okozukai.Frontend/
        ├── vite.config.ts       # D-06 — server.proxy added
        └── src/api/client.ts    # D-05 — baseURL becomes relative
```

### Pattern 1: Multi-stage Dockerfile combining Node build + .NET publish
**What:** Three named stages — Node builds the SPA, .NET SDK publishes the API, a slim `aspnet` runtime image copies both outputs. Layer-cache ordering copies dependency manifests (`package.json`/`package-lock.json`, `*.csproj`) before source, so `npm ci`/`dotnet restore` layers only invalidate when dependencies change, not on every source edit.
**When to use:** Any repo where a non-.NET build tool must produce an artifact (`dist/`) that the final .NET image needs to embed.
**Example:**
```dockerfile
# Source: pattern synthesized from Microsoft's own multi-stage guidance
# [CITED: learn.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images]
# combined with the .NET 8+ non-root $APP_UID convention
# [CITED: devblogs.microsoft.com/dotnet/running-nonroot-kubernetes-with-dotnet — "$APP_UID
#  environment variable set in the runtime-deps base image... app:x:1654:1654"]

# ---- Stage 1: build the SPA ----
FROM node:22-slim AS frontend-build
WORKDIR /src
COPY src/Okozukai.Frontend/package.json src/Okozukai.Frontend/package-lock.json ./
RUN npm ci
COPY src/Okozukai.Frontend/ ./
RUN npm run build
# Produces /src/dist — see Code Examples for exact file list verified this session

# ---- Stage 2: publish the API ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY Okozukai.slnx ./
COPY src/Okozukai.Api/Okozukai.Api.csproj src/Okozukai.Api/
COPY src/Okozukai.Application/Okozukai.Application.csproj src/Okozukai.Application/
COPY src/Okozukai.Domain/Okozukai.Domain.csproj src/Okozukai.Domain/
COPY src/Okozukai.Infrastructure/Okozukai.Infrastructure.csproj src/Okozukai.Infrastructure/
COPY src/Okozukai.ServiceDefaults/Okozukai.ServiceDefaults.csproj src/Okozukai.ServiceDefaults/
RUN dotnet restore src/Okozukai.Api/Okozukai.Api.csproj
COPY src/Okozukai.Api/ src/Okozukai.Api/
COPY src/Okozukai.Application/ src/Okozukai.Application/
COPY src/Okozukai.Domain/ src/Okozukai.Domain/
COPY src/Okozukai.Infrastructure/ src/Okozukai.Infrastructure/
COPY src/Okozukai.ServiceDefaults/ src/Okozukai.ServiceDefaults/
RUN dotnet publish src/Okozukai.Api/Okozukai.Api.csproj -c Release -o /app/publish --no-restore

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=backend-build /app/publish .
COPY --from=frontend-build /src/dist ./wwwroot
USER $APP_UID
ENTRYPOINT ["dotnet", "Okozukai.Api.dll"]
```
Note: `Okozukai.AppHost`, `Okozukai.Web` (empty leftover [VERIFIED: `Okozukai.slnx` — not listed as a project in the solution at all]), and `tests/` are deliberately **not** copied — `dotnet restore src/Okozukai.Api/Okozukai.Api.csproj` only pulls in its actual project references (Application, Domain, Infrastructure, ServiceDefaults) [VERIFIED: src/Okozukai.Api/Okozukai.Api.csproj:19-22 — the four `<ProjectReference>` entries], so nothing else needs to be in the build context for this stage.

### Pattern 2: Static-file + SPA-fallback middleware ordering, safe against a missing `wwwroot`
**What:** `UseStaticFiles()` (with `OnPrepareResponse` for cache headers) registered once; `MapControllers()` then `MapFallbackToFile("index.html")` — the exact order D-01 specifies.
**When to use:** This phase's ORIG-03/D-01 requirement, and specifically: this ordering must not break the 22 integration tests that boot through `WebApplicationFactory` from source, where `src/Okozukai.Api/wwwroot` never exists.
**Example:**
```csharp
// Source: pattern combining D-01's locked ordering with framework behavior verified
// this session (see Common Pitfalls Pitfall 1 for the full safety argument)
app.UseExceptionHandler();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        if (path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            // Vite content-hashed filenames (e.g. /assets/index-x7TlS8Fk.js) never
            // change content under the same name — safe to cache for a year.
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        }
        else
        {
            // index.html, sw.js, registerSW.js, manifest.webmanifest, workbox-*.js —
            // anything the browser/service worker must re-check on every deploy.
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
        }
    }
});

app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");
```
**Why this is safe with no `wwwroot`:** `UseStaticFiles()` with no explicit `FileProvider`/path override resolves `IWebHostEnvironment.WebRootFileProvider`, which defaults to the `wwwroot` convention. When that folder doesn't exist on disk (as in the integration-test source tree) [VERIFIED: `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs` does not call `UseWebRoot()` or set `WebRootPath` anywhere — grep-confirmed no such call in the file], no exception is thrown at startup — the middleware simply has no files to serve and every static-file/fallback request falls through to a 404 [CITED: github.com/dotnet/AspNetCore.Docs issue #15578 discussion, cross-checked against learn.microsoft.com/aspnet/core/fundamentals/static-files]. This is a **different** code path from `DirectoryNotFoundException`-at-startup issue [CITED: github.com/dotnet/aspnetcore#48620], which is specifically triggered by an app calling `builder.WebHost.UseWebRoot("some/custom/path")` where that custom path doesn't exist — **this phase's `Program.cs` must not call `UseWebRoot`/set a custom `WebRootPath`**, and none of the locked decisions ask for one; letting the default `wwwroot` convention resolve is what makes both the container (where it's populated by the Docker `COPY`) and the test run (where it's simply absent) work with identical code.

### Pattern 3: `compose.yaml` — two services, named volume, health-gated startup
**What:** `db` (postgres:17) + `app` (this repo's Dockerfile), `app` depends on `db` being healthy, `env_file: .env` supplies secrets at runtime (PKG-05).
**Example:**
```yaml
# Source: pattern combining docker-library/postgres official guidance
# [CITED: github.com/docker-library/docs/blob/master/postgres/README.md] with
# the Phase 1 health contract (D-09 here / D-12 in 01-CONTEXT.md)
services:
  db:
    image: postgres:17
    environment:
      POSTGRES_USER: okozukai
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: okozukai
    volumes:
      - pgdata:/var/lib/postgresql/data   # NOT /var/lib/postgresql — parent path does not persist
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U okozukai -d okozukai"]
      interval: 5s
      timeout: 5s
      retries: 10
    restart: unless-stopped

  app:
    build: .
    depends_on:
      db:
        condition: service_healthy
    env_file: .env
    ports:
      - "8080:8080"     # only the app service publishes a port — ACC-03 forward constraint
    restart: unless-stopped

volumes:
  pgdata:
```
Note: `ConnectionStrings__okozukai` (Phase 1 D-13) belongs in `.env` (gitignored) alongside `POSTGRES_PASSWORD`, `ASPNETCORE_ENVIRONMENT=Production`, and `ASPNETCORE_URLS=http://+:8080` — the `.env.example` from Phase 1 already documents the API-side variables [CITED: `.planning/phases/01-production-readiness/01-RESEARCH.md`'s own `.env.example` code example]; this phase's `.env.example` update adds `POSTGRES_PASSWORD` and `POSTGRES_USER`/`POSTGRES_DB` if those need to be parameterized, and the connection string's `Password=` value must match `POSTGRES_PASSWORD` **exactly**, quoting rules included (see Common Pitfalls Pitfall 5).

### Pattern 4: Vite dev-server proxy reading Aspire's service-discovery env var
**What:** `server.proxy` in `vite.config.ts` forwards `/api` to the API's address during `aspire run`, reading the env var Aspire's service discovery already injects via the existing `frontend.WithReference(api)` call — no new AppHost wiring needed beyond deleting the `VITE_API_URL`/`TAILNET_API_PORT` lines D-06 already calls for.
**Example:**
```typescript
// Source: Vite server.proxy shape is textbook [CITED: vite.dev proxy docs, cross-checked
// via WebSearch]; the env var name follows Aspire's documented service-discovery
// convention [CITED: aspire.dev/fundamentals/service-discovery — "services__[serviceName]
// __[endpointName]__[index]", explicitly demonstrated for a Node/TypeScript consumer
// reading process.env.services__basket__https__0]
export default defineConfig({
  server: {
    port: process.env.PORT ? parseInt(process.env.PORT) : 5173,
    strictPort: true,
    host: true,
    proxy: {
      '/api': {
        target: process.env.services__api__http__0 ?? 'http://localhost:5005',
        changeOrigin: true,
      },
    },
  },
  // ...
})
```
`frontend.WithReference(api)` at `src/Okozukai.AppHost/Program.cs:25` [VERIFIED: line already present, unconditional, not inside the `tailnetIp` branch] is what causes Aspire to inject `services__api__http__0` (endpoint name `"http"` matches the name already used at `api.WithEndpoint("http", ...)` in the same file) into the Vite dev-server process's environment when `aspire run` starts it — this is a build/dev-time-only Node `process.env` read inside `vite.config.ts`, never exposed to the browser bundle (only `import.meta.env.VITE_*` variables reach client code, and none is used here). **Confidence: MEDIUM** — the exact env var name for an `AddViteApp`-wrapped resource specifically (as opposed to a generic `AddProject`) was not shown verbatim in official docs during this session's search; the format itself (`services__{name}__{endpoint}__{index}`) is confirmed to apply uniformly across resource/consumer types. The `?? 'http://localhost:5005'` fallback keeps the dev proxy working even if the env var name turns out to need adjustment — treat as a plan-time verification point (start `aspire run`, `console.log(process.env)` in `vite.config.ts` once, confirm the exact variable name before relying on it).

### Anti-Patterns to Avoid
- **Using `MapStaticAssets()` for the SPA's `wwwroot` content:** silently serves nothing for the frontend's `dist/` files, because they're copied into `wwwroot` after the API's own `dotnet publish` generates its static-assets manifest. Use `UseStaticFiles()`.
- **Calling `builder.WebHost.UseWebRoot(...)` or setting a custom `WebRootPath`:** this is the specific pattern that throws `DirectoryNotFoundException` at startup when the custom path doesn't exist (issue #48620) — the default `wwwroot` convention must be left alone.
- **Mounting the Postgres named volume at `/var/lib/postgresql` instead of `/var/lib/postgresql/data`:** documented to silently fail to persist data across container recreation.
- **A Dockerfile `HEALTHCHECK` or `depends_on` polling `/alive` instead of `/health`:** `/alive` is process-only (D-09) and would report healthy before Postgres is actually reachable, defeating the point of gating compose startup order.
- **Re-introducing a reverse-proxy service in `compose.yaml`:** explicitly reversed and rejected per D-01; do not re-propose.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Waiting for Postgres to accept connections at container boot | A custom wait-loop in the Dockerfile entrypoint or compose | `MigrationExtensions.ApplyDatabaseMigrations` (10× retry, 3s backoff — already exists, Phase 1) + compose `depends_on: condition: service_healthy` | Two independent, already-solved layers: compose won't even start the app container until Postgres's own `pg_isready` health check passes, and the app's existing retry loop is a second line of defense for slow migrations. |
| Serving the SPA with per-path cache policy | A middleware library or reverse-proxy cache-control config | `StaticFileOptions.OnPrepareResponse`, branching on `ctx.Context.Request.Path` | Built into ASP.NET Core, zero new dependencies, and this is exactly what D-04 specifies. |
| Composing a Postgres connection string with a special-character password | Manual string concatenation with ad-hoc escaping | Npgsql's own quoting rule: wrap the value in double quotes when it contains a semicolon; double any embedded `=` in a *keyword* (not applicable here since the password is a value, not a keyword) | Getting this wrong produces a connection string that either fails to parse or silently truncates at the first `;`/`=`, connecting with a partial/wrong password — a Phase 1 UAT-deferred item this phase must resolve. |
| Detecting whether the app should seed sample data | A new environment-variable flag distinct from `ASPNETCORE_ENVIRONMENT` | The existing `IsDevelopment()` guard (Phase 1, unchanged) + never setting `ASPNETCORE_ENVIRONMENT=Development` in `compose.yaml`/`.env` | ASP.NET Core already defaults to `Production` when the variable is absent [CITED: learn.microsoft.com/aspnet/core/fundamentals/environments] — the safest thing this phase's compose file can do for `DevSeedData`'s wipe risk is *not set the variable at all* or set it explicitly to `Production`; either is safe, no new guard code needed. |

**Key insight:** Every mechanism this phase needs (retry/backoff migrations, the readiness/liveness health split, ASP.NET Core's static-file middleware, Postgres's own first-boot database creation, Aspire's service discovery) already exists in the framework or the codebase. The work is Dockerfile/compose authoring plus a handful of small, already-scoped code edits — not new infrastructure.

## Runtime State Inventory

> Included because this phase changes the deployment topology (new container boundary) even though it is not a rename/refactor in the traditional sense — CONTEXT.md explicitly flags PostgreSQL data durability as the highest-stakes item.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | No existing production data — this is the first deployment (milestone 1, "Okozukai serves from the homelab Linux box" is the Definition of Done, not yet met). Any local/dev Postgres data (Homebrew install, per STACK.md) is separate from the container's named volume and is not migrated by this phase. | None — fresh volume, first boot. `POSTGRES_DB` creates the schema DB; Phase 1's migrations then run on top via `ApplyDatabaseMigrations`. |
| Live service config | None — no external services (n8n, Datadog, etc.) reference this app. | None. |
| OS-registered state | None — no OS-level task scheduler, pm2, or systemd registration exists yet for this app (first deployment). | None this phase — may become relevant if a future phase adds a systemd unit for the compose stack; out of scope here. |
| Secrets/env vars | `ConnectionStrings__okozukai` (Phase 1 D-13) moves from being read directly by the API process to being supplied via compose's `env_file: .env` — same variable name, same read path (`IConfiguration`), no code change. A new `POSTGRES_PASSWORD` variable is introduced for the `db` service and **must match** the password embedded in `ConnectionStrings__okozukai`'s `Password=` segment. | Code edit: none. Data/config discipline: both values must be kept in sync manually in `.env` — flag this as a verification point (see Verification Architecture below). |
| Build artifacts | No prior Docker image, compose file, or `.dockerignore` exists — this phase creates all three from scratch [VERIFIED: `find` for these files at repo root returned nothing before this session's work]. | None to migrate — greenfield within this category. |

## Common Pitfalls

### Pitfall 1: Static-file middleware breaking the 22 integration tests (the phase's highest-risk item)
**What goes wrong:** A plan step adds `UseStaticFiles()`/`MapFallbackToFile()` in a way that throws at app startup — either because it explicitly overrides `WebRootPath`/calls `UseWebRoot()` pointing at a path that doesn't exist in the test's content root, or because it uses `MapStaticAssets()` (which reads a build-time manifest) instead of `UseStaticFiles()`.
**Why it happens:** Two similarly-named GitHub issues get conflated: #15578 ("wwwroot doesn't exist, does nothing bad") describes the *safe* default-convention case; #48620 ("DirectoryNotFoundException") describes a *different*, unsafe pattern — an app explicitly calling `UseWebRoot()`/setting `WebRootPath` to a custom directory that doesn't exist at the time `WebHost.Build()` runs.
**How to avoid:** Do not call `UseWebRoot()` or set `WebApplicationOptions.WebRootPath` anywhere in `Program.cs`. Let the default `wwwroot` convention resolve — it will be populated in the Docker image (via `COPY --from=frontend-build`) and absent (safely) in the test-boot case, where `CustomWebApplicationFactory` uses the source tree as its content root [VERIFIED: `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs` contains no `UseWebRoot`/`WebRootPath` override — confirmed by reading the full file this session]. Use `UseStaticFiles()`, not `MapStaticAssets()`, for the reason in the Standard Stack Alternatives table.
**Warning signs:** `dotnet test` fails at `WebApplicationFactory` construction (not at a specific test) with `DirectoryNotFoundException` — that specific failure mode is the #48620 pattern; if it appears, check for an accidental `UseWebRoot`/`WebRootPath` addition.
**Verification:** `dotnet test Okozukai.slnx --no-build -nologo` (per AGENTS.md's own documented test command) must show 22/22 passing after the `Program.cs` changes land, run *before* any Dockerfile work, purely against the source tree with no `wwwroot` present.

### Pitfall 2: Postgres named volume mounted at the wrong path silently loses data
**What goes wrong:** `compose.yaml` mounts the named volume at `/var/lib/postgresql` (the parent directory) instead of `/var/lib/postgresql/data` (the actual `PGDATA` for `postgres:17`). The container appears to work — Postgres starts, the app connects — but on `docker compose down && docker compose up` (or any container recreation), all data is gone because the volume was never actually backing the directory Postgres writes to.
**Why it happens:** The path looks plausible (it *is* a real directory inside the container) and the mistake produces no error at any point — this is the single most dangerous failure mode in the phase because it is silent until the container is recreated, by which point real data may already exist.
**How to avoid:** Mount exactly `pgdata:/var/lib/postgresql/data`. This is `postgres:17`'s fixed `PGDATA` path (not version-specific — that's an 18+ change).
**Warning signs:** None until recreation — this is why the verification step below must explicitly test a down/up cycle, not just an initial `up`.
**Verification:** Create a journal + transaction via the API, `docker compose down` (without `-v`), `docker compose up -d`, confirm via `GET /api/journals` that the data is still present. This exact cycle is the only way to catch this pitfall before it happens for real.

### Pitfall 3: `MapFallbackToFile` swallowing genuine 404s for `/api/*` routes it shouldn't touch
**What goes wrong:** A plan step registers `MapFallbackToFile("index.html")` *before* `MapControllers()`, or with a route pattern broader than its default, causing an `/api/nonexistent-endpoint` request to fall through to `index.html` (200 OK with HTML) instead of a proper 404.
**Why it happens:** Misunderstanding of `MapFallbackToFile`'s actual matching semantics — its default pattern is `{*path:nonfile}` with `Order = int.MaxValue`, meaning it always loses to any other matched endpoint regardless of registration order in code, but a plan author unfamiliar with this might add explicit ordering hints that break the assumption.
**How to avoid:** Use `app.MapFallbackToFile("index.html")` with no custom route pattern or order override, registered after `app.MapControllers()` (matching D-01's exact wording) for code readability, even though the actual endpoint-matching behavior doesn't strictly depend on that ordering.
**Warning signs:** A request to a genuinely nonexistent API path (e.g. a typo'd route during manual testing) returns HTML instead of a 404 JSON `ProblemDetails` payload.
**Verification:** `curl -i http://localhost:8080/api/does-not-exist` should return `404` with a `ProblemDetails` JSON body (via `GlobalExceptionHandler`'s existing `KeyNotFoundException` → 404 mapping, or ASP.NET Core's own unmatched-route 404), not `200` with HTML.

### Pitfall 4: `POSTGRES_DB`/`CREATEDB` assumption drift between Phase 1 and Phase 2
**What goes wrong:** Phase 1's `3D000` auto-create fallback [VERIFIED: `src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs:39-53`] assumes the app's Postgres role has `CREATEDB`. If `compose.yaml`'s `POSTGRES_DB` doesn't match the database name in `ConnectionStrings__okozukai`'s `Database=` segment, the app connects successfully (the role exists) but the migration's first `Database.Migrate()` call fails with `3D000` (database doesn't exist), triggering the fallback — which will actually work in this topology (because `POSTGRES_USER` set to a custom name gets superuser/`CREATEDB` automatically per the official image's own entrypoint behavior), but this is now doing real provisioning work at every fresh deploy instead of `POSTGRES_DB` doing it once, cleanly, at container first-boot.
**Why it happens:** Two independent configuration surfaces (`compose.yaml`'s `POSTGRES_DB` env var and `.env`'s `ConnectionStrings__okozukai` connection string) both name the same database, with nothing enforcing they agree.
**How to avoid:** Set `POSTGRES_DB` in `compose.yaml` to the exact same value as the `Database=` segment of `ConnectionStrings__okozukai` in `.env` (recommend `okozukai` for both, matching the existing local-dev convention implied by the connection string key name `okozukai`). Document this pairing requirement directly in `.env.example`'s comments.
**Warning signs:** Migration logs show `"--> Attempting to create database..."` on what should be a fresh, empty-volume first boot — that log line firing at all on a truly first boot indicates `POSTGRES_DB` and the connection string's database name disagree.
**Verification:** On first `docker compose up` against a fresh named volume, `docker compose logs app | grep "Attempting to create database"` should return **nothing** — `POSTGRES_DB` should have already created it, so the fallback path should never fire on a clean deploy.

### Pitfall 5: Npgsql connection-string quoting for a password containing `;` or `=`
**What goes wrong:** A generated or manually-typed password containing a semicolon or equals sign is placed unquoted into `ConnectionStrings__okozukai=Host=db;Port=5432;Database=okozukai;Username=okozukai;Password=abc;123`. Npgsql parses this as `Password=abc` followed by a bare, invalid `123` token (or silently truncates the password at the `;`), producing either a parse error or — worse — a successful connection with a truncated password that happens to still authenticate against a similarly-truncated actual password, masking the bug.
**Why it happens:** Connection-string format is `keyword1=value1;keyword2=value2` — any `;` inside a *value* must be escaped or the parser treats it as a new keyword=value pair boundary.
**How to avoid:** Wrap any value containing a semicolon in double quotes: `Password="abc;123"` [CITED: npgsql.org/doc/connection-string-parameters.html — "Values containing special characters (e.g. semicolons) can be double-quoted"]. This interacts with `.env`/compose's own variable-substitution: compose's `env_file:` reads `.env` as raw `KEY=VALUE` lines and does **not** apply shell quoting rules — the double quotes must be literal characters inside the connection-string *value* (i.e., part of what Npgsql sees), not shell-style quoting around the whole `.env` line. If `.env` also uses `$`-prefixed variable *interpolation* elsewhere in `compose.yaml` (e.g. `${POSTGRES_PASSWORD}`), be aware compose interpolates `$VAR`/`${VAR}` syntax in the *compose file* itself, not inside `.env` file contents referenced via `env_file:` — so a raw `$` character inside a password stored only in `.env` (never referenced via `${...}` in `compose.yaml`) is not compose-interpolated and passes through literally, but a `$` character would need doubling (`$$`) only if the value is used inside `compose.yaml` directly rather than solely via `env_file:`.
**Warning signs:** The app logs an Npgsql parse exception at startup, or (more dangerous) authenticates successfully in a way that doesn't match the operator's expectation — always test with the *actual* homelab password (or a value containing similar special characters) during D-05-style manual verification, not a placeholder without special characters.
**Verification:** Generate a test password containing both `;` and `=` (e.g. via `openssl rand -base64 24` and manually check/inject those characters if the random output doesn't happen to include them), set it as both `POSTGRES_PASSWORD` and inside `ConnectionStrings__okozukai`'s `Password="..."` (quoted) in `.env`, `docker compose up`, and confirm `GET /health` reports the Postgres check healthy.

## Code Examples

### Verified `npm run build` output (this session, `src/Okozukai.Frontend`)
```
dist/index.html                          # no hash — must be no-cache
dist/manifest.webmanifest                # no hash — must be no-cache
dist/registerSW.js                       # no hash — must be no-cache
dist/sw.js                               # no hash — must be no-cache (service worker script itself)
dist/workbox-8c29f6e4.js                 # hashed — safe to cache long, but harmless to no-cache too
dist/vite.svg                            # static, unhashed favicon asset — low-stakes either way
dist/assets/index-Dfa8mZJd.css           # content-hashed — safe to cache 1yr immutable
dist/assets/index-x7TlS8Fk.js            # content-hashed — safe to cache 1yr immutable
dist/assets/DashboardPage-DWmhKnNl.js    # content-hashed (lazy-loaded route chunk) — safe to cache 1yr immutable
```
[VERIFIED: `npx vite build` executed this session in `src/Okozukai.Frontend`, output read via `Read` tool on `dist/index.html` and `ls -la dist/ dist/assets/`] — filenames are illustrative (hashes change on every build), but the **structural pattern** (`/assets/*` is always content-hashed; everything at `dist/` root is never hashed) is what the `OnPrepareResponse` path-branch in Pattern 2 above relies on, and that pattern is stable across builds — confirmed by inspecting `dist/index.html`'s own generated `<script src="/assets/index-x7TlS8Fk.js">` reference, which is how the browser discovers the hashed filename in the first place.

### `.dockerignore`
```
# Source: pattern combining official .NET/Node samples, cross-checked via WebSearch
# [CITED: github.com/dockersamples/dotnet-album-viewer/.dockerignore pattern style]
**/bin/
**/obj/
**/node_modules/
**/dist/
**/dist-ssr/
**/test-results/
**/playwright-report/
.git/
.gsd/
.planning/
.vs/
.vscode/
tests/
*.md
.env
.env.*
!.env.example
```
Note the `**/` prefix (not a bare `bin`/`node_modules`) — this repo has multiple `.csproj` projects each with their own `bin/`/`obj/`, so the recursive-anywhere form is required, not just a root-level pattern.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `UseStaticFiles()` scanning disk at every request | `MapStaticAssets()` — manifest-driven, precomputed compression/fingerprinting | .NET 9 (Nov 2024) [CITED: learn.microsoft.com/aspnet/core/fundamentals/static-files] | **Not adopted this phase** — despite being the newer/faster API, its build-time-manifest model is incompatible with SPA files copied into `wwwroot` post-publish (see Standard Stack Alternatives table). This is a deliberate divergence from the "use the newest API" default, not an oversight. |
| `POSTGRES_DB`'s fixed `PGDATA` at `/var/lib/postgresql/data` for all versions | Version-specific `PGDATA` (e.g. `/var/lib/postgresql/18/docker`) | `postgres:18` image (2025) [CITED: hub.docker.com/_/postgres] | Does not affect this phase (locked to `postgres:17`, D-02) but is a forward-compat note: a future major-version bump of the `postgres:` tag would require also updating the volume mount path in `compose.yaml`. |
| Chrome ignoring service-worker script HTTP cache headers only after 24h | Chrome 68+ ignores HTTP cache entirely for SW script update checks by default | Chrome 68 (2018) [CITED: developer.chrome.com/blog/fresher-sw] | Means an un-set `Cache-Control` on `sw.js` is *less* dangerous than commonly assumed in modern Chrome — but explicit `no-cache` (this phase's D-04) is still the correct choice for immediacy and cross-browser consistency, and is the actual mitigation for `index.html` (whose staleness is not covered by any browser-level SW-specific safety net). |

**Deprecated/outdated:** None specific to this phase's core stack — `net10.0`, Vite 7, `postgres:17`, and `vite-plugin-pwa` 1.2.0 are all current as of this research date.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The exact environment-variable name `services__api__http__0` for an `AddViteApp`-wrapped Node resource reading Aspire's injected service-discovery variable — the general `services__{name}__{endpoint}__{index}` format is confirmed by official docs for Node/TypeScript consumers generally, but no example showed this exact combination (`AddViteApp` + auto-injected, non-explicit `WithEnvironment`) verbatim | Architecture Patterns Pattern 4 | Low — this only affects the dev-time (`aspire run`) proxy target, not the production compose path (which has no Vite dev server at all). The code example includes a `?? 'http://localhost:5005'` fallback, and the plan should add a one-time manual verification step (`console.log(process.env)` inside `vite.config.ts` during `aspire run`) before relying on the exact name. |
| A2 | `node:22-slim` as the recommended Docker build-stage base image — this is a reasonable inference from the locally-installed Node major version (`v22.18.0`) and Vite 7's stated floor (20.19+/22.12+), not a value pinned anywhere in the repo (no `.nvmrc`/`global.json`/`engines` field exists) | Standard Stack | Low — any Node 20.19+ or 22.12+ image works per Vite's own documented requirement; `22` matches what's already proven to work locally. Recommend the plan add a `.nvmrc` or `package.json engines` field as a low-priority follow-up so this stops being an assumption for future work. |
| A3 | `POSTGRES_USER` set to a non-default value is granted superuser (and therefore `CREATEDB`) by the official image's entrypoint — sourced from WebSearch snippets describing the image's `initdb`-time role creation, not a verbatim quote from the official Docker Hub README fetched this session | Common Pitfalls Pitfall 4 | Low-Medium — if wrong, the `3D000` fallback (Phase 1, D-03) might not have `CREATEDB` privilege and would fail cleanly (logs "Database creation failed", falls through to retry/crash) rather than silently misbehaving; the *primary* path (`POSTGRES_DB` creating the DB at first boot) does not depend on this claim at all, so this only matters if `POSTGRES_DB` and the connection string's database name are ever out of sync (the exact scenario Pitfall 4 already tells the plan to prevent). |

## Open Questions

1. **Exact Aspire service-discovery env var name for the Vite dev-proxy target (A1 above).**
   - What we know: the general format and that it works uniformly across consumer languages including Node/TypeScript, per official docs.
   - What's unclear: whether `AddViteApp`'s specific wrapping changes the injected variable name versus a plain `AddProject`/`AddExecutable` reference.
   - Recommendation: the plan should include a one-line manual check (`aspire run`, inspect the frontend process's environment or add a temporary `console.log` in `vite.config.ts`) as the first step of implementing D-06, before writing the proxy config against an assumed name.

2. **Should `.env.example` be updated in this phase to add `POSTGRES_PASSWORD`/`POSTGRES_USER`/`POSTGRES_DB`?**
   - What we know: Phase 1's `.env.example` (per its own RESEARCH.md) documents only the API-side variables (`ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `ConnectionStrings__okozukai`). This phase introduces `db`-service-specific variables that `compose.yaml` needs.
   - What's unclear: whether the plan should treat `.env.example` as an update target (in-scope, since PKG-05 is this phase's requirement) — CONTEXT.md's "Files this phase modifies" list does not explicitly mention `.env.example`, only `compose.yaml`, `Dockerfile`, `.dockerignore`, and the four `Program.cs`/`client.ts`/`vite.config.ts`/`AppHost/Program.cs` edits.
   - Recommendation: treat `.env.example` as in-scope for this phase (it is the natural place to document `POSTGRES_PASSWORD` and the `POSTGRES_DB`/connection-string pairing from Pitfall 4) even though CONTEXT.md's file list didn't enumerate it — this is a low-risk addition consistent with PKG-05's intent, not a new decision requiring re-litigation.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|--------------|-----------|---------|----------|
| .NET SDK | Build/publish stage, `dotnet test` verification | ✓ | 10.0.100 [VERIFIED: `dotnet --version` this session] | — |
| Node.js / npm | Frontend build, `npm run build` verification | ✓ | v22.18.0 / npm 11.16.0 [VERIFIED: `node --version`/`npm --version` this session] | — |
| Docker Engine (CLI) | Building/running the image and compose stack | ✓ (client) | 29.6.1 [VERIFIED: `docker --version` this session] | — |
| Docker Engine (daemon) | Actually running `docker build`/`docker compose up` for verification | ✗ in this research sandbox — `docker info` fails to reach the daemon socket [VERIFIED: this session's `docker info` output: "failed to connect to the docker API at unix:///Users/archer.chang/.docker/run/docker.sock... no such file or directory"] | — | Start Docker Desktop (or the daemon) before attempting any `docker build`/`docker compose up` verification step; the compose plugin itself is present [VERIFIED: `~/.docker/cli-plugins/docker-compose` symlink exists] so no install is needed, only the running daemon. This is a sandbox-specific gap, not expected to apply on the actual homelab deployment target. |
| PostgreSQL (local, for comparison) | Not required by this phase (Postgres now runs in `db`'s container) | ✓ (Homebrew, per Phase 1 research) | 17.10 [CITED: `.planning/phases/01-production-readiness/01-RESEARCH.md`] | N/A — this phase's Postgres is exclusively the `postgres:17` container |

**Missing dependencies with no fallback:** None — the one gap (Docker daemon not running) has a trivial fallback (start it) and does not block writing correct Dockerfile/compose content, only executing the verification steps in this sandbox.
**Missing dependencies with fallback:** Docker daemon (start Docker Desktop before verification).

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|--------------------|
| V2 Authentication | No | Out of scope this milestone (Tailnet isolation is the accepted model, per `.planning/REQUIREMENTS.md` §Out of Scope) |
| V3 Session Management | No | No sessions introduced |
| V4 Access Control | No | No authorization logic touched |
| V5 Validation, Sanitization and Encoding | No new surface | This phase adds no new input-handling code; `OnPrepareResponse`'s path branching reads `HttpContext.Request.Path`, an already-validated ASP.NET Core routing value, not user-supplied free text |
| V9 Communication | Yes | D-01 collapses two origins into one, which is what makes ORIG-02's CORS removal *correct* rather than merely convenient — there is genuinely no cross-origin traffic left in the deployed topology to protect against. Plain HTTP inside the container network is unchanged from Phase 1 (D-07 there), relying on Tailscale's WireGuard encryption at the network layer. |
| V14 Configuration | Yes | PKG-05's `.env`/`env_file` mechanism is a direct continuation of Phase 1's D-13/D-16; `POSTGRES_PASSWORD` joins `ConnectionStrings__okozukai` as a secret that must stay out of git (already covered by the existing `.gitignore` `.env` entries — no new gitignore work needed, same conclusion Phase 1's research reached for its own secrets). |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|------------------------|
| `/health` and `/alive` now reachable on the same published port as the app (D-08, accepted risk) | Information Disclosure (minor — reveals process/DB connectivity state) | **Explicitly accepted, not mitigated, per D-08.** Do not propose `RequireHost`, auth, or a management port — this was weighed and declined by the user. Recorded here for completeness, not as an open item. |
| Postgres data loss from a volume-mount-path mistake (Pitfall 2) | — (not a STRIDE category; an availability/integrity failure, not an attack) | Mount exactly `/var/lib/postgresql/data`; verify with an explicit down/up cycle before considering PKG-04 done. |
| Connection-string password truncation from unescaped `;`/`=` (Pitfall 5) | Tampering (self-inflicted misconfiguration, not an external attacker) | Double-quote the password value per Npgsql's documented escaping rule; test with a password containing both characters during verification, not a placeholder. |
| `docker inspect` exposing `ConnectionStrings__okozukai` (including the DB password) via `env_file`-supplied env vars | Information Disclosure | **Unchanged accepted risk from Phase 1 (R-01)** — `env_file:` in compose has the identical exposure profile to the plain env var Phase 1 already accepted; this phase does not make it worse or better. Not re-litigated. |

## Sources

### Primary (HIGH confidence)
- Direct source reads this session: `src/Okozukai.Api/Program.cs`, `src/Okozukai.Api/Okozukai.Api.csproj`, `src/Okozukai.Frontend/package.json`, `src/Okozukai.Frontend/vite.config.ts`, `src/Okozukai.Frontend/src/api/client.ts`, `src/Okozukai.Frontend/playwright.config.ts`, `src/Okozukai.Frontend/e2e/dashboard.spec.ts`, `src/Okozukai.AppHost/Program.cs`, `src/Okozukai.AppHost/Okozukai.AppHost.csproj`, `src/Okozukai.ServiceDefaults/Extensions.cs`, `src/Okozukai.Infrastructure/Persistence/MigrationExtensions.cs`, `src/Okozukai.Infrastructure/Persistence/DevSeedData.cs`, `tests/Okozukai.IntegrationTests/CustomWebApplicationFactory.cs`, `Okozukai.slnx`, `.gitignore`, `AGENTS.md`, `.planning/codebase/STACK.md`, `.planning/phases/01-production-readiness/01-CONTEXT.md`, `.planning/phases/01-production-readiness/01-RESEARCH.md`.
- Direct command execution this session: `dotnet --version`, `node --version`, `npm --version`, `docker --version`, `docker info`, `docker compose version`, `npx vite build` (in `src/Okozukai.Frontend`, output verified via `ls`/`Read` on `dist/`), `grep` for `[Route]`/`[EnableCors]` attributes across `src/Okozukai.Api/Controllers/`, `find` for `global.json`/`.nvmrc`/`nuget.config`/`Directory.Build.props`.

### Secondary (MEDIUM confidence — WebSearch/WebFetch cross-checked against official documentation)
- [Static files in ASP.NET Core (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0) — `OnPrepareResponse` pattern, `MapStaticAssets` manifest-driven behavior and its incompatibility with post-publish file copies, `WebRootPath` default-convention behavior.
- [UseStaticFiles still needed with UseFileServer for wwwroot — dotnet/AspNetCore.Docs#15578](https://github.com/dotnet/AspNetCore.Docs/issues/15578) — confirms no exception when `wwwroot` doesn't exist under the default convention.
- [DirectoryNotFoundException when setting a custom WebRoot at runtime — dotnet/aspnetcore#48620](https://github.com/dotnet/aspnetcore/issues/48620) — confirms this is a distinct, avoidable failure mode tied specifically to explicit `UseWebRoot()` calls.
- [postgres — Official Image, docker-library/docs README](https://github.com/docker-library/docs/blob/master/postgres/README.md) and [Docker Hub postgres page](https://hub.docker.com/_/postgres) — `POSTGRES_DB` first-boot behavior, `PGDATA` mount path requirement, version-specific path change starting at `postgres:18`.
- [Npgsql connection string parameters](https://www.npgsql.org/doc/connection-string-parameters.html) — semicolon-in-value double-quoting rule.
- [.NET Aspire service discovery (aspire.dev, redirected from learn.microsoft.com)](https://aspire.dev/fundamentals/service-discovery/) — `services__{name}__{endpoint}__{index}` format, confirmed uniform across .NET/Go/Python/Node consumers.
- [Set up JavaScript apps in the AppHost (aspire.dev)](https://aspire.dev/integrations/frameworks/javascript/) — `AddViteApp`'s automatic `PORT` injection; did not show the exact auto-injected endpoint-reference variable name for a `WithReference()`-only (non-`WithEnvironment`) wiring — hence Assumption A1.
- [ASP.NET Core runtime environments (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments?view=aspnetcore-10.0) — `Production` is the default when `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT` are unset.
- [Running non-root .NET containers with Kubernetes (.NET Blog)](https://devblogs.microsoft.com/dotnet/running-nonroot-kubernetes-with-dotnet/) — `$APP_UID` convention (1654, user `app`) built into .NET 8+ container images.
- [Fresher service workers, by default (Chrome for Developers)](https://developer.chrome.com/blog/fresher-sw) — Chrome 68+ HTTP-cache-bypass behavior for service-worker script update checks.
- Vite 7 release notes/migration guide (multiple sources cross-checked via WebSearch, e.g. vite.dev/blog/announcing-vite7) — Node 20.19+/22.12+ requirement.
- `dotnet publish /t:PublishContainer` limitation (codewithmukesh.com, milanjovanovic.tech, cross-checked against the general shape of Microsoft's own container-tools documentation) — cannot run non-.NET build steps, ruling it out for this phase's combined Node+dotnet requirement.

### Tertiary (LOW confidence)
- `POSTGRES_USER` superuser-grant claim (Assumption A3) — sourced from WebSearch result snippets describing the image's entrypoint behavior, not a verbatim quote fetched from the official README this session. Flagged in Assumptions Log; low practical risk because the primary provisioning path (`POSTGRES_DB`) doesn't depend on it.
- Exact `AddViteApp`-specific service-discovery env var name (Assumption A1) — flagged for one-time manual verification during implementation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all repo-internal versions confirmed by direct file reads this session; base image recommendations are standard, current, and cross-checked against official Microsoft/Docker documentation.
- Architecture: HIGH — the two highest-risk patterns (static-file/wwwroot safety, Postgres volume path) are backed by direct reads of the relevant framework GitHub issues and official image documentation, not inference alone.
- Pitfalls: HIGH — Pitfall 1 (the phase's single biggest risk, per CONTEXT.md's own framing) is grounded in a direct read of `CustomWebApplicationFactory.cs` confirming the absence of any `UseWebRoot`/`WebRootPath` override, cross-referenced against two distinct, verified framework behaviors.
- The two lowest-confidence items (Aspire's exact env var name for `AddViteApp` resources, and the `POSTGRES_USER` superuser-grant detail) are both isolated to low-blast-radius decisions with documented fallbacks/verification steps — see Assumptions Log A1/A3.

**Research date:** 2026-08-21
**Valid until:** 30 days (stable stack; the one fast-moving element — Vite's minor-version release cadence — does not affect this phase's Node-version floor conclusion, which is tied to the already-pinned `^7.3.1`)
