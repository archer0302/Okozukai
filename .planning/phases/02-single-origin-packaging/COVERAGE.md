# Phase 2: Single-Origin Packaging — API Coverage Declaration

**Detector result:** `detected: false` over `02-CONTEXT.md` + the ROADMAP phase section at plan
time; `detected: true` over the three PLAN bodies (six signals, all false positives — enumerated
below).

**Declaration:**

No external API integration: this phase packages Okozukai's own ASP.NET Core application and Vue
SPA into container images, collapses the browser's two origins into one, and adds a compose stack
with PostgreSQL. It adds no client of any third-party API, SDK, or service.

## Why the detector fires (false positives)

| Signal | Snippet source | Why it is not an external API integration |
|--------|----------------|-------------------------------------------|
| verb `wiring`/`consumes` + noun `api` | `02-02-PLAN.md`: *"`apiPort` … the API's own `ASPNETCORE_URLS` wiring"* | "the API" throughout this phase is Okozukai's own `src/Okozukai.Api` project. `apiPort` is an Aspire dev-time port number for that project, not an endpoint of a third party. |
| verb `connect` + noun `api` | `02-02-PLAN.md`: *"…the dev API in this task connect to it rather than to the compose stack's container"* | The connection is between this repository's own API process and its own PostgreSQL database, through the EF Core provider that has been installed since before this milestone. |
| noun `endpoint` + verb `wiring` | `02-02-PLAN.md`: AppHost endpoint configuration | Aspire resource endpoints for this repository's own two processes. No outbound call to any external service exists in the phase. |
| noun `sdk` (surface) | `02-01-PLAN.md`: *"under the Web SDK's implicit usings"* and the `mcr.microsoft.com/dotnet/sdk:10.0` build image | The .NET SDK is the build toolchain and the MSBuild SDK attribute of a `.csproj`, not an integrated service SDK. |
| noun `api` (surface) | `02-02-PLAN.md`: *"Replace the 'Frontend API base URL comes from Aspire' bullet"* | A documentation string being deleted, describing this repository's own API. Removing it is the task. |

## Supporting evidence

- `02-RESEARCH.md` `## Package Legitimacy Audit` records **"Not applicable this phase. No new
  NuGet or npm packages are installed."**
- The only new external artifacts are four first-party base images —
  `node:22-slim`, `mcr.microsoft.com/dotnet/sdk:10.0`,
  `mcr.microsoft.com/dotnet/aspnet:10.0`, `postgres:17` — pulled by the container runtime, not
  installed from a package registry, and therefore outside the slopsquatting surface the
  legitimacy gate targets.
- `files_modified` across the three plans is: `src/Okozukai.Api/Program.cs`,
  `src/Okozukai.Frontend/src/api/client.ts`, `src/Okozukai.Frontend/vite.config.ts`,
  `src/Okozukai.AppHost/Program.cs`, `.env.example`, four documentation files, and three new
  root-level infrastructure files. No client module, no credential exchange with a third party,
  no outbound HTTP anywhere.
- This phase *removes* a cross-origin surface (the permissive CORS policy) rather than adding an
  integration.

**Re-run the coverage gate** if a revision introduces an actual external service client — a
container registry push, a TLS terminator's management API, a secrets manager, or a backup target.
