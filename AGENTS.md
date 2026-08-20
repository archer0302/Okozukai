# AGENTS.md — Okozukai

Instructions for AI coding agents working in this repository. This is the single
agent instruction file; it is read by Claude Code, GitHub Copilot, Codex, Cursor,
and other tools that follow the AGENTS.md convention.

## Build, test, and run

### .NET backend
- Build all projects: `dotnet build Okozukai.slnx -nologo`
- Run full test suite: `dotnet test Okozukai.slnx --no-build -nologo`
- Run a single test: `dotnet test tests/Okozukai.UnitTests/Okozukai.UnitTests.csproj --filter "FullyQualifiedName~TransactionTests.Create_Throws_WhenAmountIsNotPositive" -nologo`
- Format: `dotnet format Okozukai.slnx` (not wired into CI yet)

### Vue frontend (`src/Okozukai.Frontend`)
- Install deps: `npm install`
- Run tests: `npm test` (Vitest, runs `vitest run`)
- E2E tests: `npm run test:e2e` (Playwright, requires a running app)
- Build: `npm run build`

### Running the full application
- `aspire run` — starts PostgreSQL + API + Vue frontend via the .NET Aspire AppHost
- Requires a reachable PostgreSQL instance (local install is fine, no container needed); the API auto-migrates the DB on startup in Development
- Only restart if `src/Okozukai.AppHost/Program.cs` changes

## Project overview

Okozukai is a self-hosted personal budget tracker: a clean-architecture modular
monolith backend with a decoupled Vue 3 SPA frontend.

**Status:** Feature-complete and covered by tests. There is no active development
phase and no roadmap file — see "Source of truth" below.

## Architecture

**Backend (.NET 10, ASP.NET Core):**
- `src/Okozukai.Domain` — `Journal` (Id, Name, PrimaryCurrency, IsClosed), `Transaction` (JournalId FK), `Tag`. `TransactionType` enum (In/Out). Static factory methods, private constructors, inline validation. No external dependencies.
- `src/Okozukai.Application` — `TransactionService`, `JournalService`, `TagService` (use cases); `ITransactionRepository` / `IJournalRepository` / `ITagRepository` (interfaces); `Contracts/` DTOs.
- `src/Okozukai.Infrastructure` — EF Core `OkozukaiDbContext`, repository implementations, code-first migrations, `MigrationExtensions` for resilient startup.
- `src/Okozukai.Api` — `TransactionsController`, `JournalsController`, `TagsController` delegate to services; `GlobalExceptionHandler` maps exceptions to HTTP responses.
- `src/Okozukai.ServiceDefaults` — shared OpenTelemetry and health-check configuration.
- `src/Okozukai.AppHost` — Aspire orchestrator: wires PostgreSQL → API → Vue npm app; injects `VITE_API_URL` into the frontend.

**Frontend (`src/Okozukai.Frontend`, Vue 3 + TypeScript + Vite):**
- `src/api/` — `client.ts` (Axios instance), `transactionService.ts`, `journalService.ts`
- Tailwind CSS v4 for styling. No component library and no Pinia.
- **Vue Router is used.** `src/router/index.ts` defines two routes:
  - `/` → `components/TransactionDashboard.vue` (transaction list and CRUD)
  - `/dashboard` → `components/dashboard/DashboardPage.vue` (charts, lazy-loaded)
- `src/App.vue` is the shell: journal selector, dark-mode toggle, journal create/edit modal. It shares state with descendants via `provide()` (`currentJournal`, `journals`, `isDark`).
- Chart components live in `components/dashboard/` (Chart.js via vue-chartjs).
- Journal selection persists as `lastSelectedJournalId` in `localStorage`.
- Component tests: Vitest + `@vue/test-utils` with jsdom.

## API surface

**Journals** (`/api/journals`):
- `GET /api/journals` — list (id, name, primaryCurrency, isClosed, createdAt)
- `GET /api/journals/{id}`
- `POST /api/journals` — create (name, primaryCurrency)
- `PUT /api/journals/{id}` — update (name)
- `DELETE /api/journals/{id}` — only when `isClosed=true` (409 otherwise); cascade-deletes transactions
- `POST /api/journals/{id}/close` and `POST /api/journals/{id}/reopen`

**Transactions** (`/api/transactions`) — all endpoints require `?journalId=`:
- `GET /api/transactions` — list with filters: `journalId`, `from`, `to`, `tagIds[]`, `noteSearch`, `page`, `pageSize`
- `GET /api/transactions/{id}` — read one
- `GET /api/transactions/summary` — balance object (totalIn, totalOut, net, currency)
- `GET /api/transactions/grouped` — year/month grouped view with rollups
- `GET /api/transactions/spending-by-tag` — spending breakdown by tag
- `GET /api/transactions/spending-by-tag-monthly` — per-month breakdown by tag
- `GET /api/transactions/export` — CSV export
- `POST /api/transactions` — create (journalId, type, amount, occurredAt, note?, tagIds[])
- `PUT /api/transactions/{id}` — update
- `DELETE /api/transactions/{id}?journalId=`

**Tags** (`/api/tags`):
- `GET /api/tags` — list (id, name, color)
- `GET /api/tags/{id}` — read one
- `POST /api/tags` / `PUT /api/tags/{id}` / `DELETE /api/tags/{id}`

**Error responses:** every error goes through `GlobalExceptionHandler`, which returns
an ASP.NET `ProblemDetails` payload — `{ "title", "status", "detail", "instance", "traceId" }`
(`instance` is the request path, `traceId` the `HttpContext.TraceIdentifier`).
Mapping: `ArgumentException` → 400, `ArgumentOutOfRangeException` → 400,
`KeyNotFoundException` → 404, `InvalidOperationException` → 409, anything else → 500.

## Aspire workflow

- For diagnostics, use **list structured logs**, **list console logs**, and **list traces** before editing code.
- When adding an Aspire integration: use **list integrations** to find the version, then **get integration docs**. Match the version to `Aspire.AppHost.Sdk`.
- Avoid persistent containers early in development to prevent state issues on restart.
- Never install or use the Aspire workload (it is obsolete).
- Docs: https://aspire.dev and https://learn.microsoft.com/dotnet/aspire

## Key conventions

- **Domain validation lives in the domain layer.** `Transaction` validates amount (> 0) and note (trimmed/nulled). `Journal` validates name (required) and PrimaryCurrency (3-letter ISO). Controllers must not re-implement these rules.
- **DTOs live in `Okozukai.Application.Contracts`.** Never expose domain entities from the API; map at the service layer.
- **One journal = one currency.** Currency is a journal-level concept. Never merge balances across currencies and never convert implicitly.
- **Repository interfaces are the persistence boundary.** Only `Okozukai.Infrastructure` touches EF Core; controllers and services depend on interfaces.
- **`GlobalExceptionHandler` is the single error-mapping point.** Do not add per-controller try/catch.
- **Frontend API base URL comes from Aspire.** `VITE_API_URL` is injected by the AppHost at dev time; `src/api/client.ts` reads it via `import.meta.env`.
- **Source of truth:** `README.md` documents the feature set, API surface, and known limitations; `.planning/codebase/` holds the generated architecture map. There is no roadmap or phase plan — keep both current when behaviour changes.
