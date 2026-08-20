# External Integrations

**Analysis Date:** 2026-08-19

## APIs & External Services

**None detected**

This is a self-hosted application with no external API dependencies. All functionality is provided by internal services.

## Data Storage

**Databases:**
- PostgreSQL (version 13+)
  - Connection: `ConnectionStrings:okozukai` in user secrets
  - Client: Npgsql via Entity Framework Core 10.0.3
  - EF Core provider: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0
  - Database context: `OkozukaiDbContext` (`src/Okozukai.Infrastructure/Persistence/OkozukaiDbContext.cs`)
  - Schema management: Code-first migrations via EF Core
  - Tables: `Journals`, `Transactions`, `Tags`
  - Auto-migration on startup: Yes (development only)

**Connection Configuration:**
- Development: User secrets
  ```
  dotnet user-secrets set "ConnectionStrings:okozukai" \
    "Host=localhost;Port=5432;Database=okozukai;Username=postgres;Password=yourpassword" \
    --project src/Okozukai.AppHost
  ```
- Aspire AppHost: Connection string reference: `builder.AddConnectionString("okozukai")`

**File Storage:**
- Local filesystem only — no cloud file storage
- CSV export feature generates in-memory files on-demand (`src/Okozukai.Api/Controllers/TransactionsController.cs` — `Export` endpoint)

**Caching:**
- None configured — all queries hit PostgreSQL directly

## Authentication & Identity

**Auth Provider:**
- Custom / None
- Implementation: No authentication middleware configured
- All API endpoints are public (CORS allows any origin)
- No user identity/authorization framework
- Designed as single-user or trusted-network application

**CORS Configuration:**
- Policy: Allow all origins, methods, headers
- Location: `src/Okozukai.Api/Program.cs` line 13-21

## Monitoring & Observability

**Error Tracking:**
- OpenTelemetry Protocol (OTLP) - Optional export
  - Exporter: `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.14.0
  - Configured via `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable
  - Implementation: `src/Okozukai.ServiceDefaults/Extensions.cs` lines 81-88
  - Azure Monitor (commented out, available if uncommented with `Azure.Monitor.OpenTelemetry.AspNetCore` package)

**Logs:**
- Approach: Structured logging via Microsoft.Extensions.Logging
- Configuration: `appsettings.json` in `src/Okozukai.Api` and `src/Okozukai.AppHost`
  - Default level: Information
  - ASP.NET Core logs: Warning
- Output: Console and OpenTelemetry (if OTLP endpoint configured)

**Tracing:**
- OpenTelemetry tracing enabled
- Instrumentation:
  - ASP.NET Core HTTP requests (excludes `/health` and `/alive` endpoints)
  - HTTP client calls (service-to-service)
  - .NET runtime metrics
- Configuration: `src/Okozukai.ServiceDefaults/Extensions.cs` lines 47-79

**Metrics:**
- OpenTelemetry metrics collection enabled
- Instrumented:
  - ASP.NET Core request metrics
  - HTTP client metrics
  - .NET runtime metrics
- Exported via OTLP if endpoint configured

**Health Checks:**
- Endpoints: `/health` (ready check) and `/alive` (liveness check)
- Available in development only
- Configuration: `src/Okozukai.ServiceDefaults/Extensions.cs` lines 100-127

## CI/CD & Deployment

**Hosting:**
- Self-hosted only — no cloud provider integrations
- Local development: .NET Aspire + a local PostgreSQL instance
- Deployment: Any environment with .NET 10 runtime and PostgreSQL

**CI Pipeline:**
- Not configured — no GitHub Actions or CI workflows present
- Manual test execution via `dotnet test` and npm commands

**Build & Run:**
- Entry point: `src/Okozukai.AppHost/Program.cs` (Aspire host)
- Run command: `aspire run`
- Default Aspire dashboard: Opens automatically on port (varies)
- Frontend served: Vite dev server on port 5173 (or environment variable `PORT`)
- API served: ASP.NET Core on port 5005 (or `TAILNET_API_PORT`)

**Deployment Considerations:**
- Tailscale VPN integration available via `TAILNET_IP` environment variable
- Port configuration: `TAILNET_API_PORT`, `TAILNET_FRONTEND_PORT`
- Frontend requires npm build step: `npm run build` (pre-configured in `src/Okozukai.Frontend/package.json`)
- Database migrations: Auto-applied on API startup (development mode only)

## Environment Configuration

**Required env vars:**
- `ConnectionStrings:okozukai` - PostgreSQL connection string
- `ASPNETCORE_ENVIRONMENT` - Application environment (Development/Production)

**Optional env vars:**
- `VITE_API_URL` - Frontend API base URL (default: `http://localhost:5005`)
- `OTEL_EXPORTER_OTLP_ENDPOINT` - OpenTelemetry export endpoint
- `PORT` - Frontend server port (default: 5173)
- `TAILNET_IP` - Tailscale IP address for remote access
- `TAILNET_API_PORT` - API port on Tailnet (default: 5005)
- `TAILNET_FRONTEND_PORT` - Frontend port on Tailnet (default: 5173)

**Secrets location:**
- .NET user secrets (development): `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
  - UserSecretsId: `2244fb95-a254-4de6-85f0-482e9faa5f7d` (in `src/Okozukai.AppHost/Okozukai.AppHost.csproj`)
- Environment variables (production): System/container environment

**No committed secrets:** `.env*` files are not present or committed

## Webhooks & Callbacks

**Incoming:**
- None — application is not a webhook receiver

**Outgoing:**
- None — application does not send webhooks to external services

## API Documentation

**OpenAPI/Swagger:**
- Enabled: Yes
- Framework: `Microsoft.AspNetCore.OpenApi` 10.0.3
- Endpoint: `/openapi/v1.json` (development only)
- UI: Swagger UI (development only) — requires manual setup if desired
- Available during development to document RESTful endpoints

**Endpoints:**
- Base URL: `http://localhost:5005/api` (or configured `VITE_API_URL`)
- Journals: `/journals`, `/journals/{id}`, `/journals/{id}/close`, `/journals/{id}/reopen`
- Transactions: `/transactions`, `/transactions/{id}`, `/transactions/summary`, `/transactions/grouped`, `/transactions/spending-by-tag`, `/transactions/spending-by-tag-monthly`, `/transactions/export`
- Tags: `/tags`, `/tags/{id}`

**Error Responses:**
- Format: JSON with `message` and `detail` fields
- Handler: `GlobalExceptionHandler` middleware (`src/Okozukai.Api/Middlewares/GlobalExceptionHandler.cs`)
- Mapping:
  - `KeyNotFoundException` → 404 Not Found
  - `InvalidOperationException` → 409 Conflict
  - `ArgumentException` → 400 Bad Request
  - Other exceptions → 500 Internal Server Error

---

*Integration audit: 2026-08-19*
