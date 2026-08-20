# Okozukai (お小遣い)

A self-hosted personal budget tracker built with a clean-architecture .NET 10 backend and a Vue 3 SPA frontend, orchestrated with .NET Aspire.

Every feature listed below is implemented and covered by tests. There is no active development phase in progress.

## Screenshots

| Light mode | Dark mode |
|---|---|
| ![Light mode](light-mode.png) | ![Dark mode](dark-mode.png) |

**Dashboard with charts:**

![Dashboard](dashboard-final.png)

## Features

- **Journals** – Organize finances into independent budget contexts, each with its own currency
- **Transactions** – Record income and expenses with notes, dates, and tags
- **Tags** – Categorize spending with colour-coded labels and filter/search by tag
- **Spending by tag** – See where money goes with bar charts and doughnut charts
- **Monthly charts** – Income vs expenses, net balance trend, and per-tag monthly breakdown
- **Dashboard customization** – Toggle individual chart panels; preferences are persisted per journal
- **Period grouping** – Transactions grouped by year/month with collapsible rollup summaries
- **CSV export** – Export transactions for a journal to a CSV file
- **Dark mode** – Full dark-mode support across all pages and charts
- **Close/reopen journals** – Archive completed budget periods; closed journals are read-only

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| API | ASP.NET Core Web API |
| ORM | Entity Framework Core + PostgreSQL |
| Orchestration | .NET Aspire |
| Frontend | Vue 3 + TypeScript + Vite |
| Styling | Tailwind CSS v4 |
| Charts | Chart.js via vue-chartjs |
| Tests | xUnit (unit + integration), Vitest, Playwright |

## Architecture

```
src/
  Okozukai.AppHost        # .NET Aspire orchestrator (entry point)
  Okozukai.Api            # ASP.NET Core controllers + global error handler
  Okozukai.Application    # Use-cases, DTOs, repository interfaces
  Okozukai.Domain         # Entities and business rules (no dependencies)
  Okozukai.Infrastructure # EF Core, PostgreSQL, code-first migrations
  Okozukai.Frontend       # Vue 3 SPA
  Okozukai.ServiceDefaults# Shared OpenTelemetry / health-check config
tests/
  Okozukai.UnitTests       # Domain + application unit tests
  Okozukai.IntegrationTests# API integration tests (WebApplicationFactory)
```

### Design principles

- Domain logic lives in `Okozukai.Domain`; business rules never leak into controllers
- DTOs in `Okozukai.Application.Contracts` are mapped at the service layer — domain entities are never exposed by the API
- `ITransactionRepository` / `IJournalRepository` / `ITagRepository` are the persistence boundary; nothing outside `Infrastructure` touches EF Core directly
- `GlobalExceptionHandler` is the single error-mapping point: `KeyNotFoundException` → 404, `InvalidOperationException` → 409, `ArgumentException` → 400. No per-controller try/catch
- One journal = one currency; balances are never merged across currencies and there is no implicit conversion
- Write operations use explicit transaction boundaries, and data migrations avoid destructive rewrites without traceability

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Aspire CLI](https://aspire.dev) (`aspire run` orchestrates the API and frontend)
- PostgreSQL 14+ reachable from your machine — a local install (`brew install postgresql@17`) is enough; no container is required

## Getting started

```bash
# 1. Clone the repository
git clone https://github.com/archer0302/Okozukai.git
cd Okozukai

# 2. Create the database
createdb okozukai

# 3. Provide the connection string
#    The AppHost reads it from the 'okozukai' connection string.
#    User secrets keep it out of the repository:
dotnet user-secrets set "ConnectionStrings:okozukai" \
  "Host=localhost;Port=5432;Database=okozukai;Username=$(whoami)" \
  --project src/Okozukai.AppHost

# 4. Install frontend dependencies
cd src/Okozukai.Frontend && npm install && cd ../..

# 5. Run the application (starts the API + frontend)
aspire run
```

The Aspire dashboard opens automatically. The Vue frontend is available at the URL shown under the `frontend` resource.

> **Note:** The API applies EF Core migrations automatically on startup. No manual `dotnet ef` commands are required.

### Tailscale / Tailnet access (optional)

To expose the app on your Tailnet, set the `TAILNET_IP` environment variable to your machine's Tailscale IP before running:

```bash
export TAILNET_IP=<your-tailscale-ip>  # e.g. 100.x.x.x
aspire run --launch-profile tailnet
```

`TAILNET_API_PORT` (default `5005`) and `TAILNET_FRONTEND_PORT` (default `5173`) can also be overridden the same way.

## Running tests

```bash
# Backend (unit + integration)
dotnet test Okozukai.slnx --no-build -nologo

# Frontend (Vitest component tests)
cd src/Okozukai.Frontend && npm test

# Frontend E2E (Playwright — requires the app to be running via `aspire run`)
cd src/Okozukai.Frontend
npx playwright install chromium   # first run only, downloads the browser
npm run test:e2e                  # defaults to http://localhost:5173, override with BASE_URL
```

Current suite: **31 unit + 22 integration + 16 frontend component tests**, all passing.

## API overview

| Method | Path | Description |
|---|---|---|
| `GET/POST` | `/api/journals` | List / create journals |
| `GET/PUT/DELETE` | `/api/journals/{id}` | Read / update / delete a journal |
| `POST` | `/api/journals/{id}/close` | Close a journal (makes it read-only) |
| `POST` | `/api/journals/{id}/reopen` | Reopen a closed journal |
| `GET/POST` | `/api/transactions?journalId=` | List / create transactions |
| `PUT/DELETE` | `/api/transactions/{id}?journalId=` | Update / delete a transaction |
| `GET` | `/api/transactions/summary?journalId=` | Balance summary (totalIn, totalOut, net) |
| `GET` | `/api/transactions/grouped?journalId=` | Year/month grouped view with rollups |
| `GET` | `/api/transactions/{id}?journalId=` | Read a single transaction |
| `GET` | `/api/transactions/spending-by-tag?journalId=` | Spending breakdown by tag |
| `GET` | `/api/transactions/spending-by-tag-monthly?journalId=` | Per-month spending breakdown by tag |
| `GET` | `/api/transactions/export?journalId=` | CSV export |
| `GET/POST` | `/api/tags` | List / create tags |
| `GET` | `/api/tags/{id}` | Read a single tag |
| `PUT/DELETE` | `/api/tags/{id}` | Update / delete a tag |

All error responses go through `GlobalExceptionHandler` and return an ASP.NET
`ProblemDetails` payload: `{ "title", "status", "detail", "instance", "traceId" }`.
`ArgumentException` → 400, `KeyNotFoundException` → 404, `InvalidOperationException` → 409.

## Known limitations

- Tag create/update checks name uniqueness before saving, so concurrent writes could race past the check (low risk for a single-user app)
- Period rollups report `opening` as `0` and `closing` as the period net — they are not cumulative across periods
- `Tag.Color` is stored without domain-level format validation

## License

This project is licensed under the [MIT License](LICENSE).
