# Phase 1: Production Readiness — API Coverage Declaration

**Detector result:** `detected: true` (`api-coverage.cjs` over `01-CONTEXT.md` + `01-RESEARCH.md`)

**Declaration:**

No external API integration: this phase changes only Okozukai's own ASP.NET Core hosting
configuration (migration call site, health-endpoint gate, HTTPS-redirect middleware,
environment-variable config) and adds no client of any third-party API, SDK, or service.

## Why the detector fired (false positives)

| Signal | Snippet source | Why it is not an external API integration |
|--------|----------------|-------------------------------------------|
| verb `connects` + noun `api` | RESEARCH.md Pitfall 3: *"If the Postgres role **the API connects** as lacks the `CREATEDB` privilege…"* | "the API" here is Okozukai's own ASP.NET Core project (`src/Okozukai.Api`), not a third-party API being consumed. The connection is to the project's own PostgreSQL database through the already-installed EF Core provider — an existing dependency, unchanged by this phase. |
| noun `sdk` (surface) | RESEARCH.md Environment Availability table: *".NET SDK … 10.0.100"* | The .NET SDK is the build toolchain, not an integrated service SDK. It is listed only to record that the manual Production verification (D-05) can run locally. |

## Supporting evidence

- RESEARCH.md `## Package Legitimacy Audit` records **"Not applicable this phase. No new
  external packages are installed."**
- D-10 explicitly forbids adding a package reference or DI wiring for a database health
  check — the check already exists via `AddNpgsqlDbContext`.
- D-15 keeps the Aspire AppHost (the only orchestration surface) untouched.
- `files_modified` for the phase is `src/Okozukai.Api/Program.cs`,
  `src/Okozukai.ServiceDefaults/Extensions.cs`, and a new `.env.example` — no client
  module, no credential exchange, no outbound HTTP to any third party.

**Re-run the coverage gate** if a plan revision introduces an actual external service
client (a TLS terminator's management API, a container registry, a secrets manager).
