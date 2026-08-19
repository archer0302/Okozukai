# Codebase Structure

**Analysis Date:** 2026-08-19

## Directory Layout

```
Okozukai/                                    # Repository root
├── .claude/                                 # Claude Code configuration
│   ├── settings.json                        # Project-level settings
│   └── hooks/                               # Git hooks
├── .github/                                 # GitHub configuration
│   ├── workflows/                           # CI/CD workflows (if any)
│   └── copilot-instructions.md              # GitHub Copilot instructions
├── .planning/                               # Planning & analysis output
│   └── codebase/                            # Generated architecture docs
├── src/                                     # All application source code
│   ├── Okozukai.Api/                        # ASP.NET Core API layer
│   │   ├── Controllers/                     # HTTP endpoints
│   │   ├── Middlewares/                     # GlobalExceptionHandler
│   │   ├── Program.cs                       # API startup configuration
│   │   ├── appsettings.json                 # Configuration
│   │   └── Okozukai.Api.csproj              # Project file
│   ├── Okozukai.Application/                # Business logic & use cases
│   │   ├── Contracts/                       # DTOs (Request/Response)
│   │   ├── Transactions/                    # Services & Repository interfaces
│   │   ├── DependencyInjection.cs           # DI extension method
│   │   └── Okozukai.Application.csproj      # Project file
│   ├── Okozukai.Domain/                     # Domain entities & rules
│   │   ├── Transactions/                    # Domain models
│   │   └── Okozukai.Domain.csproj           # Project file
│   ├── Okozukai.Infrastructure/             # EF Core & persistence
│   │   ├── Persistence/                     # DbContext, Repositories
│   │   │   ├── Configurations/              # EF entity mappings
│   │   │   ├── Migrations/                  # Code-first migrations
│   │   │   ├── Repositories/                # Repository implementations
│   │   │   ├── OkozukaiDbContext.cs         # EF Core context
│   │   │   └── MigrationExtensions.cs       # Auto-migration startup
│   │   ├── DependencyInjection.cs           # DI extension method
│   │   └── Okozukai.Infrastructure.csproj   # Project file
│   ├── Okozukai.Frontend/                   # Vue 3 SPA frontend
│   │   ├── src/                             # TypeScript/Vue source
│   │   │   ├── api/                         # API client (journalService, transactionService)
│   │   │   ├── components/                  # Vue components
│   │   │   │   ├── dashboard/               # Chart components
│   │   │   │   ├── AddTransactionForm.vue   # Transaction form
│   │   │   │   └── TransactionDashboard.vue # Main dashboard
│   │   │   ├── router/                      # Vue Router configuration
│   │   │   ├── types/                       # TypeScript type definitions
│   │   │   ├── tests/                       # Vitest unit tests
│   │   │   ├── assets/                      # Static files (CSS, images)
│   │   │   ├── App.vue                      # Root component
│   │   │   ├── main.ts                      # Bootstrap entry point
│   │   │   └── style.css                    # Global Tailwind CSS
│   │   ├── public/                          # Static assets (served as-is)
│   │   ├── e2e/                             # Playwright E2E tests
│   │   ├── package.json                     # npm dependencies
│   │   ├── tsconfig.json                    # TypeScript configuration
│   │   ├── vite.config.ts                   # Vite build configuration
│   │   └── vitest.config.ts                 # Vitest test configuration
│   ├── Okozukai.ServiceDefaults/            # Shared observability setup
│   │   ├── ServiceDefaults.cs               # OpenTelemetry & health checks
│   │   └── Okozukai.ServiceDefaults.csproj  # Project file
│   ├── Okozukai.AppHost/                    # .NET Aspire orchestration
│   │   ├── Program.cs                       # Orchestration definition
│   │   ├── grafana/                         # Grafana dashboard config
│   │   │   ├── provisioning/                # Grafana data sources
│   │   │   └── dashboards/                  # Pre-built dashboards
│   │   ├── Properties/                      # Launch profiles
│   │   └── Okozukai.AppHost.csproj          # Project file
│   └── Okozukai.Web/                        # (Legacy/unused, minimal content)
├── tests/                                   # Automated test suites
│   ├── Okozukai.UnitTests/                  # Domain + application unit tests
│   │   └── Transactions/                    # Test fixtures & test classes
│   └── Okozukai.IntegrationTests/           # API integration tests (WebApplicationFactory)
├── Okozukai.slnx                            # Solution file (SLNX format)
├── README.md                                # Project documentation
├── LICENSE                                  # MIT License
└── .gitignore                               # Git ignore rules
```

## Directory Purposes

**`.claude/`:**
- Purpose: Claude Code configuration, settings, hooks
- Contains: settings.json (project permissions/config), hooks/package.json (hook dependencies)

**`.github/`:**
- Purpose: GitHub-specific configuration
- Contains: Copilot instructions, workflows

**`.planning/`:**
- Purpose: Generated planning documents and analysis
- Contains: Architecture docs (ARCHITECTURE.md, STRUCTURE.md, etc.)

**`src/Okozukai.Api/`:**
- Purpose: ASP.NET Core HTTP API layer
- Contains: Controllers (route handlers), Middlewares (GlobalExceptionHandler), configuration, entry point
- Key files: `Program.cs` (startup), `Controllers/*.cs` (HTTP routes)

**`src/Okozukai.Application/`:**
- Purpose: Application services (use cases) and DTO contracts
- Contains: DTOs (Contracts/), Services (Transactions/), DI registration
- Key files: `DependencyInjection.cs`, `Transactions/*Service.cs`, `Contracts/*.cs`

**`src/Okozukai.Domain/`:**
- Purpose: Core business entities and rules (no external dependencies)
- Contains: Entities (Journal, Transaction, Tag), enums (TransactionType)
- Key files: `Transactions/Journal.cs`, `Transactions/Transaction.cs`, `Transactions/Tag.cs`

**`src/Okozukai.Infrastructure/`:**
- Purpose: Persistence layer (EF Core, PostgreSQL, repositories)
- Contains: DbContext, repository implementations, EF configurations, migrations
- Key files: `Persistence/OkozukaiDbContext.cs`, `Persistence/Repositories/*.cs`, `DependencyInjection.cs`

**`src/Okozukai.Infrastructure/Persistence/`:**
- Purpose: Data access and database configuration
- Contains: DbContext, Repositories, EF Configurations, Migrations, seed data
- Key subdirectories: `Repositories/` (IRepository implementations), `Configurations/` (EF mappings), `Migrations/` (code-first migration files)

**`src/Okozukai.Frontend/src/`:**
- Purpose: Vue 3 SPA frontend (TypeScript)
- Contains: Vue components, router, API client, types, styles
- Key directories: `components/` (Vue components), `api/` (HTTP clients), `router/` (routing), `types/` (TS definitions)

**`src/Okozukai.Frontend/src/api/`:**
- Purpose: HTTP client layer (Axios-based)
- Contains: API service wrappers (journalService, transactionService, client base)

**`src/Okozukai.Frontend/src/components/`:**
- Purpose: Reusable Vue components
- Contains: DashboardPage (main UI), chart components, forms
- Key files: `dashboard/DashboardPage.vue`, `dashboard/*Chart.vue` (chart components)

**`src/Okozukai.ServiceDefaults/`:**
- Purpose: Shared .NET observability and health check setup
- Contains: OpenTelemetry configuration, health check endpoints

**`src/Okozukai.AppHost/`:**
- Purpose: .NET Aspire orchestration (local dev orchestration + Grafana)
- Contains: Distributed application definition, Grafana configuration
- Key files: `Program.cs` (defines API + Frontend + DB + Grafana resources)

**`tests/Okozukai.UnitTests/`:**
- Purpose: Unit tests for domain and application layers
- Contains: Test fixtures, test classes for services and domain entities
- Pattern: xUnit (one test class per class being tested)

**`tests/Okozukai.IntegrationTests/`:**
- Purpose: Integration tests for API endpoints
- Contains: WebApplicationFactory setup, controller/endpoint tests
- Pattern: xUnit, uses in-memory database or test database

## Key File Locations

**Entry Points:**
- API: `src/Okozukai.Api/Program.cs` (ASP.NET Core startup; registers DI, middlewares, CORS)
- Frontend: `src/Okozukai.Frontend/src/main.ts` (Vue app bootstrap)
- Orchestration: `src/Okozukai.AppHost/Program.cs` (Aspire resource definition)

**Configuration:**
- API settings: `src/Okozukai.Api/appsettings.json`, `src/Okozukai.Api/appsettings.Development.json`
- Frontend config: `src/Okozukai.Frontend/vite.config.ts` (build), `vitest.config.ts` (tests), `tsconfig.json` (types)
- Database connection: Passed via Aspire `builder.AddConnectionString("okozukai")` or user secrets during dev

**Core Logic:**
- Domain: `src/Okozukai.Domain/Transactions/` (Journal, Transaction, Tag entities)
- Services: `src/Okozukai.Application/Transactions/` (JournalService, TransactionService, TagService)
- Repositories: `src/Okozukai.Infrastructure/Persistence/Repositories/`

**Testing:**
- Backend unit tests: `tests/Okozukai.UnitTests/Transactions/`
- Backend integration tests: `tests/Okozukai.IntegrationTests/` (use WebApplicationFactory)
- Frontend unit tests: `src/Okozukai.Frontend/src/tests/` (Vitest + @vue/test-utils)
- Frontend E2E tests: `src/Okozukai.Frontend/e2e/` (Playwright)

## Naming Conventions

**C# Files:**
- Pattern: PascalCase, one public class per file (standard .NET)
- Examples: `Journal.cs`, `JournalService.cs`, `JournalRepository.cs`, `JournalResponse.cs`

**C# Classes:**
- Services: `[Entity]Service` (JournalService, TransactionService)
- Repositories: `[Entity]Repository` (JournalRepository)
- DTOs (Request): `Create[Entity]Request`, `Update[Entity]Request`
- DTOs (Response): `[Entity]Response`
- Interfaces: `I[Entity]Repository` (IJournalRepository)
- Entities: PascalCase (Journal, Transaction, Tag)

**Directories:**
- Feature folders: PascalCase (Transactions, Contracts, Repositories)
- Support folders: lowercase or PascalCase per convention (Controllers, Middlewares, api, components)

**Vue/TypeScript Files:**
- Components: PascalCase.vue (DashboardPage.vue, SpendingPieChart.vue)
- Services: camelCase.ts (journalService.ts, transactionService.ts)
- Utilities: camelCase.ts (client.ts, router.ts)
- Types: camelCase.ts (transaction.ts) with exported interfaces/types

**Vue Components:**
- Pages: [Feature]Page.vue (DashboardPage.vue)
- Dashboard: [Chart]Chart.vue (SpendingPieChart.vue, MonthlyBarChart.vue)
- Shared: [ComponentName].vue (AddTransactionForm.vue, TransactionDashboard.vue)

**Database:**
- Tables: snake_case (journals, transactions, tags)
- Columns: snake_case (primary_currency, is_closed, created_at)

## Where to Add New Code

**New Feature (e.g., Budget Alerts):**
1. **Domain Entity:** Create `src/Okozukai.Domain/Transactions/BudgetAlert.cs` with domain logic
2. **Repository Interface:** Add `IBudgetAlertRepository` to `src/Okozukai.Application/Transactions/`
3. **Repository Implementation:** Implement in `src/Okozukai.Infrastructure/Persistence/Repositories/BudgetAlertRepository.cs`
4. **DTO Contracts:** Add request/response DTOs to `src/Okozukai.Application/Contracts/` (CreateBudgetAlertRequest, BudgetAlertResponse)
5. **Service:** Create `src/Okozukai.Application/Transactions/BudgetAlertService.cs`
6. **Controller Endpoints:** Add routes to `src/Okozukai.Api/Controllers/` (new BudgetAlertsController or add to existing)
7. **DI Registration:** Update `src/Okozukai.Application/DependencyInjection.cs` to register service
8. **Database Migration:** Create migration in `src/Okozukai.Infrastructure/Persistence/Migrations/` via `dotnet ef migrations add AddBudgetAlerts`
9. **Tests:** Add unit tests in `tests/Okozukai.UnitTests/Transactions/BudgetAlertTests.cs` and integration tests in `tests/Okozukai.IntegrationTests/`
10. **Frontend:** Create Vue components in `src/Okozukai.Frontend/src/components/` + add routes to `router/index.ts` + create API client in `api/budgetAlertService.ts`

**New API Endpoint (e.g., GET /api/transactions/spending-summary):**
1. Add method to appropriate `[Entity]Controller` in `src/Okozukai.Api/Controllers/`
2. Add corresponding method to service in `src/Okozukai.Application/Transactions/[Entity]Service.cs`
3. Create response DTO in `src/Okozukai.Application/Contracts/` if needed
4. Implement query logic in `[Entity]Repository` if needed
5. Add integration test to `tests/Okozukai.IntegrationTests/`

**New Vue Component/Page:**
1. Create `.vue` file in `src/Okozukai.Frontend/src/components/` (or `dashboard/` for chart components)
2. Add TypeScript logic in `<script setup lang="ts">`
3. Use Tailwind for styling
4. If page-level: add route to `src/Okozukai.Frontend/src/router/index.ts`
5. Use API client from `src/Okozukai.Frontend/src/api/` to fetch data
6. Add Vitest unit test in `src/Okozukai.Frontend/src/tests/`

**New Service (if business domain requires one):**
1. Create `[Feature]Service.cs` in `src/Okozukai.Application/Transactions/`
2. Inject repository interfaces via constructor
3. Add to DI in `src/Okozukai.Application/DependencyInjection.cs` as `.AddScoped<[Feature]Service>()`
4. Use ILogger for structured logging
5. Map domain entities to DTOs before returning from service methods

**Unit Test:**
- Location: `tests/Okozukai.UnitTests/Transactions/[Entity]Tests.cs`
- Framework: xUnit (`[Fact]` for simple cases, `[Theory]` with `[InlineData]` for parameterized)
- Pattern: Arrange-Act-Assert; use domain factories (Journal.Create) not constructors

**Integration Test:**
- Location: `tests/Okozukai.IntegrationTests/[Controller]Tests.cs`
- Pattern: Inherit WebApplicationFactory<Program>; use HttpClient to call endpoints
- Database: Use test database (in-memory or dedicated test DB per README)

**Frontend E2E Test:**
- Location: `src/Okozukai.Frontend/e2e/` (Playwright)
- Pattern: Test full user workflows (create journal, add transactions, view dashboard)

## Special Directories

**`src/Okozukai.Infrastructure/Persistence/Migrations/`:**
- Purpose: EF Core code-first migration files
- Generated: Yes (via `dotnet ef migrations add`)
- Committed: Yes (checked into git)
- Pattern: Each migration is a numbered file (20260219111426_InitialCreate.cs) + Designer.cs snapshot
- **Never manually edit migration files** — create new ones via `dotnet ef migrations add`

**`src/Okozukai.Infrastructure/Persistence/Configurations/`:**
- Purpose: EF Core entity mapping configuration (Fluent API)
- Generated: No
- Committed: Yes
- Pattern: One file per entity (JournalConfiguration.cs), implements IEntityTypeConfiguration<Journal>
- Applied automatically in OnModelCreating via ApplyConfigurationsFromAssembly

**`src/Okozukai.Frontend/node_modules/`:**
- Purpose: npm dependencies (JavaScript/TypeScript packages)
- Generated: Yes (by npm install)
- Committed: No (in .gitignore)
- Do not edit; manage via package.json and package-lock.json

**`src/Okozukai.AppHost/grafana/`:**
- Purpose: Grafana provisioning and dashboard definitions
- Generated: No
- Committed: Yes
- Subdirectories:
  - `provisioning/`: Grafana data sources (PostgreSQL connection)
  - `dashboards/`: Pre-built Grafana dashboard JSON files

**`.planning/codebase/`:**
- Purpose: Generated architecture and planning documents
- Generated: Yes (by `/gsd-map-codebase` and similar tools)
- Committed: Yes (reference docs for future work)

**`tests/`:**
- Purpose: All automated tests (unit, integration, E2E)
- Committed: Yes
- Structure mirrors `src/` for related tests

---

*Structure analysis: 2026-08-19*
