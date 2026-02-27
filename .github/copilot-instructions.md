# Copilot Instructions for Okozukai

## Build, test, and lint commands

### .NET backend
- Build all projects: `dotnet build Okozukai.slnx -nologo`
- Run full test suite: `dotnet test Okozukai.slnx --no-build -nologo`
- Run a single test: `dotnet test tests/Okozukai.UnitTests/Okozukai.UnitTests.csproj --filter "FullyQualifiedName~TransactionTests.Create_Throws_WhenAmountIsNotPositive" -nologo`
- Format: `dotnet format Okozukai.slnx` (not wired into CI yet)

### Vue frontend (`src/Okozukai.Frontend`)
- Install deps: `npm install` (from `src/Okozukai.Frontend`)
- Run tests: `npm test` (Vitest, runs `vitest run`)
- Build: `npm run build`

### Running the full application
- `aspire run` — starts PostgreSQL container + API + Vue frontend via .NET Aspire AppHost
- Docker Desktop must be running; the API auto-migrates the DB on startup
- Only restart if `src/Okozukai.AppHost/Program.cs` changes

## Project overview

Okozukai is a personal budget tracker built with a clean-architecture modular monolith backend and a decoupled Vue 3 SPA frontend.

**Status:** Phases 1–5 complete. Phase 5 introduced journal-scoped budget contexts, replacing flat multi-currency transactions. Each journal has a single currency; Exchange transactions were removed.

## High-level architecture

**Backend (.NET 10, ASP.NET Core):**
- `src/Okozukai.Domain` — `Journal` (Id, Name, PrimaryCurrency, IsClosed), `Transaction` (JournalId FK, no currency/exchange fields), `Tag` entities. `TransactionType` enum (In/Out). Static factory methods, private constructors, inline validation.
- `src/Okozukai.Application` — `TransactionService`, `JournalService`, `TagService` (use cases), `ITransactionRepository`/`IJournalRepository`/`ITagRepository` (interfaces), and `Contracts/` DTOs.
- `src/Okozukai.Infrastructure` — EF Core `OkozukaiDbContext`, repository implementations, code-first migrations, `MigrationExtensions` for resilient startup.
- `src/Okozukai.Api` — `TransactionsController`, `JournalsController`, and `TagsController` delegate to services; `GlobalExceptionHandler` maps domain exceptions to HTTP responses.
- `src/Okozukai.AppHost` — Aspire orchestrator: wires PostgreSQL container → API → Vue npm app; exposes `VITE_API_URL` env var to frontend.

**Frontend (`src/Okozukai.Frontend`, Vue 3 + TypeScript + Vite):**
- Axios-based `transactionService.ts` and `journalService.ts` in `src/api/`
- Tailwind CSS v4 for styling (no component library, no Pinia, no Vue Router — single-page app)
- Component tests: Vitest + `@vue/test-utils` with jsdom
- E2E tests: Playwright (`npm run test:e2e`, requires running app)
- Entry point: `src/App.vue` (journal selector + CRUD) → `src/components/TransactionDashboard.vue` (scoped to selected journal)
- Journal selector persists `lastSelectedJournalId` in `localStorage`

**Orchestration:** `src/Okozukai.AppHost` is the single entry point for local dev. The active frontend is `Okozukai.Frontend`.

## Directory structure

- `src/Okozukai.AppHost` — .NET Aspire orchestrator
- `src/Okozukai.ServiceDefaults` — Shared service configurations (OpenTelemetry, HealthChecks)
- `src/Okozukai.Api` — Web API project
- `src/Okozukai.Application` — Use cases, DTOs, and interfaces
- `src/Okozukai.Domain` — Core domain entities and business rules
- `src/Okozukai.Infrastructure` — EF Core persistence and repositories
- `src/Okozukai.Frontend` — Vue 3 SPA (Vite + Tailwind)
- `tests/Okozukai.UnitTests` — Domain and application tests (xUnit)
- `tests/Okozukai.IntegrationTests` — API integration tests

## Current API surface (Phase 5)

**Journals** (`/api/journals`):
- `GET /api/journals` — list all journals (id, name, primaryCurrency, isClosed, createdAt)
- `GET /api/journals/{id}`
- `POST /api/journals` — create (name, primaryCurrency)
- `PUT /api/journals/{id}` — update (name)
- `DELETE /api/journals/{id}` — only allowed when `isClosed=true` (409 otherwise); cascade-deletes all transactions
- `POST /api/journals/{id}/close`
- `POST /api/journals/{id}/reopen`

**Transactions** (`/api/transactions`) — all endpoints require `?journalId=`:
- `GET /api/transactions` — list with filters: `journalId`, `from`, `to`, `tagIds[]`, `noteSearch`, `page`, `pageSize`
- `GET /api/transactions/summary` — single balance object (totalIn, totalOut, net, currency)
- `GET /api/transactions/grouped` — year/month grouped view with rollups
- `GET /api/transactions/spending-by-tag` — single spending object with currency and items
- `GET /api/transactions/export` — CSV export scoped to journalId
- `POST /api/transactions` — create (journalId, type, amount, occurredAt, note?, tagIds[])
- `PUT /api/transactions/{id}` — update (type, amount, occurredAt, note?, tagIds[])
- `DELETE /api/transactions/{id}?journalId=`

**Tags** (`/api/tags`):
- `GET /api/tags` — list all tags (id, name, color)
- `POST /api/tags` — create tag (name)
- `PUT /api/tags/{id}` — update tag (name)
- `DELETE /api/tags/{id}`

**Error responses:** All errors go through `GlobalExceptionHandler` → structured JSON `{ message, detail }`. `KeyNotFoundException` → 404. `InvalidOperationException` → 409. Domain `ArgumentException` → 400.



## Aspire workflow
- For diagnostics, use **list structured logs**, **list console logs**, **list traces** before editing code.
- When adding a new Aspire integration: use **list integrations** to find the correct version, then **get integration docs** for usage details. Match the integration version to `Aspire.AppHost.Sdk`.
- Avoid persistent containers early in development to prevent state management issues on restart.
- Never install or use the Aspire workload (it is obsolete).
- Official docs: https://aspire.dev and https://learn.microsoft.com/dotnet/aspire

## Key conventions

- **Domain validation lives in the domain layer.** `Transaction` validates amount (must be > 0) and note (trimmed/nulled). `Journal` validates name (required) and PrimaryCurrency (3-letter ISO). Controllers must not re-implement these rules.
- **DTOs are in `Okozukai.Application.Contracts`.** Never expose domain entities directly from the API. Map at the service layer.
- **One journal = one currency.** Currency is a journal-level concept (Phase 5). Never merge balances across currencies.
- **`ITransactionRepository` / `ITagRepository` are the persistence boundaries.** All DB access goes through interfaces in `Okozukai.Infrastructure`. Controllers and application services depend on interfaces, not EF Core directly.
- **`GlobalExceptionHandler` is the single error-mapping point.** Domain exceptions are caught here and translated to structured API error responses — do not add per-controller try/catch.
- **Frontend API base URL comes from Aspire.** `VITE_API_URL` is injected by the AppHost at dev time; `src/Okozukai.Frontend/src/api/client.ts` reads it via `import.meta.env`.
- **Product roadmap is in `plan.md`.** New features should align with phase boundaries.
