<!-- refreshed: 2026-08-19 -->
# Architecture

**Analysis Date:** 2026-08-19

## System Overview

```text
┌──────────────────────────────────────────────────────────────┐
│                   Client (Vue 3 SPA)                         │
│        `src/Okozukai.Frontend/src` (TypeScript/Vue)          │
│                                                               │
│  Router → Pages → Components → API Client (Axios)           │
└──────────────────────────────┬───────────────────────────────┘
                               │
                               │ HTTP/JSON
                               ▼
┌──────────────────────────────────────────────────────────────┐
│            API Layer (ASP.NET Core Controllers)              │
│          `src/Okozukai.Api/Controllers`                      │
│      [JournalsController, TransactionsController,            │
│       TagsController]                                         │
│                                                               │
│  GlobalExceptionHandler → Exception Mapping (400/404/409)   │
└──────────────────────────────┬───────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────┐
│       Application Layer (Business Logic Services)            │
│        `src/Okozukai.Application/Transactions`               │
│   [JournalService, TransactionService, TagService]           │
│                                                               │
│  • Service + DTO Mapping (FromDomain pattern)               │
│  • Repository abstraction (Interface contracts)              │
│  • Logging via ILogger                                       │
└──────────────────────────────┬───────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────┐
│       Domain Layer (Business Rules & Entities)               │
│        `src/Okozukai.Domain/Transactions`                    │
│    [Journal, Transaction, Tag entities + value types]        │
│                                                               │
│  • Domain validation (no nulls, currency ISO codes)         │
│  • Business logic (Close/Reopen journals)                   │
│  • No external dependencies                                  │
└──────────────────────────────┬───────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────┐
│    Infrastructure Layer (EF Core, PostgreSQL, Migrations)   │
│     `src/Okozukai.Infrastructure/Persistence`                │
│                                                               │
│  DbContext → Repository Implementations → EF Core → DB      │
│  • OkozukaiDbContext (DbSet<Journal/Transaction/Tag>)       │
│  • Repository classes (JournalRepository, etc.)              │
│  • Migrations (code-first, auto-applied on startup)          │
└──────────────────────────────┬───────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────┐
│     PostgreSQL Database (Single schema)                      │
│     • Tables: journals, transactions, tags                   │
│     • Cascade deletes for journal → transactions             │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│    Orchestration (.NET Aspire)                               │
│    `src/Okozukai.AppHost/Program.cs`                         │
│                                                               │
│    • Database resource (okozukai connection)                 │
│    • API service (Okozukai.Api)                             │
│    • Frontend NPM app (Vue dev server)                       │
└──────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| JournalsController | HTTP endpoints for journal CRUD, close/reopen | `src/Okozukai.Api/Controllers/JournalsController.cs` |
| TransactionsController | HTTP endpoints for transaction CRUD, summary, export, grouped views, spending-by-tag | `src/Okozukai.Api/Controllers/TransactionsController.cs` |
| TagsController | HTTP endpoints for tag CRUD | `src/Okozukai.Api/Controllers/TagsController.cs` |
| GlobalExceptionHandler | Maps domain/application exceptions to HTTP status codes (400/404/409/500) | `src/Okozukai.Api/Middlewares/GlobalExceptionHandler.cs` |
| JournalService | Business logic for journal operations, DTO mapping | `src/Okozukai.Application/Transactions/JournalService.cs` |
| TransactionService | Complex transaction logic: CRUD, grouping, spending-by-tag, CSV export | `src/Okozukai.Application/Transactions/TransactionService.cs` |
| TagService | Tag CRUD, uniqueness checks, DTO mapping | `src/Okozukai.Application/Transactions/TagService.cs` |
| Journal (Entity) | Domain model: name validation, currency normalization, close/reopen state | `src/Okozukai.Domain/Transactions/Journal.cs` |
| Transaction (Entity) | Domain model: type (Income/Expense), amount, date, tag reference | `src/Okozukai.Domain/Transactions/Transaction.cs` |
| Tag (Entity) | Domain model: name, color, uniqueness rules | `src/Okozukai.Domain/Transactions/Tag.cs` |
| OkozukaiDbContext | EF Core context, entity mappings, configuration application | `src/Okozukai.Infrastructure/Persistence/OkozukaiDbContext.cs` |
| JournalRepository | Data access for journals (Get, Add, Update, Delete, SaveChanges) | `src/Okozukai.Infrastructure/Persistence/Repositories/JournalRepository.cs` |
| TransactionRepository | Data access for transactions, complex queries (grouped, spending-by-tag) | `src/Okozukai.Infrastructure/Persistence/Repositories/TransactionRepository.cs` |
| TagRepository | Data access for tags, uniqueness checks | `src/Okozukai.Infrastructure/Persistence/Repositories/TagRepository.cs` |
| Vue Router | Client-side routing (Dashboard, Transactions, Settings pages) | `src/Okozukai.Frontend/src/router/index.ts` |
| DashboardPage | Main UI: journal selector, chart panels, customization | `src/Okozukai.Frontend/src/components/dashboard/DashboardPage.vue` |
| Chart Components | Chart.js wrappers: spending pie, monthly bar, trend, monthly tag stacked bar | `src/Okozukai.Frontend/src/components/dashboard/` |

## Pattern Overview

**Overall:** Clean Architecture with clear separation of concerns

**Key Characteristics:**
- **Layered isolation:** Controllers → Services → Domain → Infrastructure (dependency flow one direction)
- **Repository pattern:** Persistence abstraction via interfaces in Application layer
- **DTO mapping:** Domain entities are never exposed by API; services map to/from contracts
- **Centralized error handling:** Single GlobalExceptionHandler for all exception-to-HTTP-status mapping
- **Transaction boundaries:** Explicit repository-level SaveChangesAsync for atomic operations
- **Domain-driven:** Business logic lives in domain entities, not services or controllers

## Layers

**Presentation Layer (API Boundary):**
- Purpose: Handle HTTP requests, validate input contracts, map responses
- Location: `src/Okozukai.Api`
- Contains: Controllers, Middlewares, appsettings.json
- Depends on: Application services, DI configuration
- Used by: Client (Vue SPA)

**Application Layer (Use Cases & DTOs):**
- Purpose: Coordinate domain logic, DTO mapping, repository abstraction
- Location: `src/Okozukai.Application`
- Contains: Services (JournalService, TransactionService, TagService), Contracts (DTOs), Repository interfaces
- Depends on: Domain entities, Repositories
- Used by: API controllers, integration tests

**Domain Layer (Business Rules):**
- Purpose: Encapsulate business logic, validation, invariants
- Location: `src/Okozukai.Domain`
- Contains: Entities (Journal, Transaction, Tag), ValueObjects, enums (TransactionType)
- Depends on: Nothing (no external dependencies)
- Used by: Application services, Infrastructure repositories

**Infrastructure Layer (Persistence & Data Access):**
- Purpose: EF Core configuration, repository implementation, database access
- Location: `src/Okozukai.Infrastructure`
- Contains: DbContext, Repository implementations, EF Configurations, Migrations, seed data
- Depends on: Domain entities, EF Core, PostgreSQL
- Used by: Application services, DI registration

**Frontend Layer (Client UI):**
- Purpose: User interface, state management, API communication
- Location: `src/Okozukai.Frontend/src`
- Contains: Vue components, Router, API client (Axios), types, assets
- Depends on: API (HTTP), Chart.js, Tailwind CSS
- Used by: End users in browser

## Data Flow

### Primary Request Path (e.g., Create Transaction)

1. **Client sends HTTP POST** to `/api/transactions` with CreateTransactionRequest body (`src/Okozukai.Frontend/src/api/transactionService.ts`)
2. **TransactionsController.Create()** receives request, calls TransactionService.CreateAsync() (`src/Okozukai.Api/Controllers/TransactionsController.cs` line 33)
3. **TransactionService.CreateAsync()** validates via domain, calls TransactionRepository.Add(), then SaveChangesAsync() (`src/Okozukai.Application/Transactions/TransactionService.cs` line 32-47)
4. **Transaction.Create()** factory validates amount, date, type per business rules (`src/Okozukai.Domain/Transactions/Transaction.cs`)
5. **TransactionRepository** persists via DbContext.Transactions.Add() and SaveChanges (EF Core executes INSERT)
6. **TransactionService** maps Transaction domain entity to TransactionResponse DTO
7. **Controller** returns 201 Created with response body
8. **Client** receives JSON, updates local state, re-queries grouped view if needed

### Grouped Transactions Request Path

1. **Client:** GET `/api/transactions/grouped?journalId=...` (`src/Okozukai.Frontend/src/components/dashboard/DashboardPage.vue` line ~120)
2. **TransactionsController.GetGroupedByPeriod()** calls TransactionService.GetGroupedByPeriodAsync() (`src/Okozukai.Api/Controllers/TransactionsController.cs` line ~75)
3. **TransactionService** fetches all transactions for journal, groups by year/month in memory, calculates rollups (opening balance, closing balance, net)
4. **TransactionRepository.GetByJournalIdAsync()** queries DB (LINQ-to-SQL compiled to PostgreSQL SELECT)
5. **Response:** IReadOnlyCollection<TransactionYearGroupResponse> with nested month groups and rollups
6. **Client:** Renders collapsible tree with monthly summaries

### Spending-by-Tag Request Path

1. **Client:** GET `/api/transactions/spending-by-tag?journalId=...`
2. **TransactionsController.GetSpendingByTag()** calls TransactionService
3. **TransactionService.GetSpendingByTagAsync()** groups transactions by tag, sums amounts by tag
4. **Response:** TransactionSpendingByTagResponse with array of tag spending items
5. **Client:** Feeds data to SpendingPieChart component (vue-chartjs)

### CSV Export Path

1. **Client:** GET `/api/transactions/export?journalId=...`
2. **TransactionsController.ExportCsv()** calls TransactionService.ExportCsvAsync()
3. **TransactionService** queries all transactions for journal, builds CSV text
4. **Response:** File download (Content-Type: text/csv)
5. **Browser:** Saves as .csv file

**State Management:**
- **Backend:** Stateless — all state in database (PostgreSQL), no in-memory caches
- **Frontend:** Component-local state (selected journal, filters, expanded groups) + HTTP cache (browser standard caching headers)
- **Transaction boundaries:** Explicit SaveChangesAsync() per operation in services; no ambient transactions
- **Concurrency:** Optimistic (no locking); last-write-wins; concurrent tag creation can race past uniqueness check (documented limitation)

## Key Abstractions

**Repository Pattern:**
- Purpose: Abstract EF Core from application layer; enable mocking in unit tests
- Examples: `IJournalRepository`, `ITransactionRepository`, `ITagRepository` in `src/Okozukai.Application/Transactions/`
- Pattern: Interface with Get/Add/Update/Delete + SaveChangesAsync; EF Core DbSet-based implementation in Infrastructure

**DTO Mapping:**
- Purpose: Transform domain entities to API contracts; decouple client from domain shape
- Examples: `JournalResponse.FromDomain(journal)`, `TransactionResponse.FromDomain(transaction)` in `src/Okozukai.Application/Contracts/`
- Pattern: Static factory methods or extension methods; explicit mapping, not auto-mapper

**Domain Entities (Aggregate Roots):**
- Purpose: Encapsulate business rules, validation, state transitions
- Examples: Journal (name validation, close/reopen), Transaction (type enum, amount > 0 checks), Tag (color, uniqueness)
- Pattern: Private constructor + static Create factory; domain logic methods (Close, Reopen); no setters for invariants

**Service Layer (Application):**
- Purpose: Coordinate between controllers and domain; handle DTO mapping, repository orchestration
- Examples: JournalService, TransactionService, TagService
- Pattern: Scoped lifetime; depends on repository interfaces and ILogger; single responsibility per service

**ValueObjects:**
- Purpose: Immutable domain concepts with value equality
- Examples: TransactionType enum, TransactionCurrencySummary record
- Pattern: Enums for fixed sets (Income/Expense); records for calculated values

## Entry Points

**API Entry Point:**
- Location: `src/Okozukai.Api/Program.cs`
- Triggers: `dotnet run` or Aspire orchestration
- Responsibilities: DI registration (AddApplication, AddInfrastructure), middleware setup, database migration on dev startup, CORS, error handling

**Frontend Entry Point:**
- Location: `src/Okozukai.Frontend/src/main.ts`
- Triggers: Vite dev server or npm run dev
- Responsibilities: Vue app bootstrap, router initialization, root component mount

**Orchestration Entry Point:**
- Location: `src/Okozukai.AppHost/Program.cs`
- Triggers: `aspire run`
- Responsibilities: Register API + Frontend + PostgreSQL resources; set environment variables; start distributed app

## Architectural Constraints

- **Layering:** No circular dependencies; Application may not reference Infrastructure concrete implementations (only interfaces), but Infrastructure implements Application interfaces.
- **Threading:** API is async-all-the-way (Task-based); CancellationToken threaded through all layers; no blocking calls (sync-over-async anti-pattern avoided).
- **Global state:** None in domain or application layers. Infrastructure has OkozukaiDbContext (scoped, recreated per request). Frontend components own their state.
- **Circular imports:** EF Core entity materializers use private parameterless constructors to bypass domain validation; compiler warns (`#pragma warning disable CS8618`).
- **Transaction scope:** Repository SaveChangesAsync commits a single transaction to the database; no explicit transaction management for multi-operation sequences (single operation = atomic).
- **Database connection:** Single PostgreSQL connection string (okozukai); no secondary databases or sharding.
- **Currency handling:** One journal = one currency; no implicit conversion; balances never merged across currencies.

## Anti-Patterns

### Exposing Domain Entities via API

**What happens:** If TransactionResponse simply returned the Transaction entity directly, clients would see internal fields.
**Why it's wrong:** Changes to domain internals (adding fields for persistence logic) would break client contracts; tight coupling prevents domain refactoring.
**Do this instead:** Map domain to DTO at service layer: `TransactionResponse.FromDomain(transaction)` in `src/Okozukai.Application/Contracts/TransactionResponse.cs` line ~15.

### Per-Controller Error Handling

**What happens:** If controllers caught exceptions individually with try/catch, error mapping would be duplicated.
**Why it's wrong:** Inconsistent HTTP status codes for same error type; harder to maintain and test.
**Do this instead:** Use GlobalExceptionHandler middleware (`src/Okozukai.Api/Middlewares/GlobalExceptionHandler.cs`); catches all exceptions and maps to ProblemDetails.

### Mixing Domain and Persistence Logic

**What happens:** If entities had EF Core attributes or SQL queries directly in domain classes.
**Why it's wrong:** Domain layer loses independence; testable without database; domain tests become integration tests.
**Do this instead:** Keep domain pure (no EF annotations); put EF configuration in Infrastructure (Configurations folder); queries in repositories.

### Bypassing Repository Abstraction

**What happens:** If a service called DbContext directly instead of through repository.
**Why it's wrong:** Makes unit testing harder; service now coupled to EF Core; persistence logic leaks into application layer.
**Do this instead:** Always go through repository interface (IJournalRepository, etc.); repository is the only class touching DbContext.

## Error Handling

**Strategy:** Domain exceptions → Application exceptions → HTTP status codes via GlobalExceptionHandler

**Patterns:**
- **ArgumentException** (domain validation failure) → 400 Bad Request ("Invalid journal name: too long")
- **KeyNotFoundException** (entity not found) → 404 Not Found ("Journal with ID ... not found")
- **InvalidOperationException** (state violation, e.g., "Cannot delete open journal") → 409 Conflict
- **Unhandled exceptions** → 500 Internal Server Error (logged, trace ID returned to client)

**Response Format:** All errors return structured JSON: `{ "title": "...", "status": 400, "detail": "...", "traceId": "..." }`

## Cross-Cutting Concerns

**Logging:** 
- ILogger<T> injected into services; logs at Information level on operations (create, update, delete), Warning on validation failures, Error on exceptions
- Configured via Serilog in ServiceDefaults (`src/Okozukai.ServiceDefaults/`)
- Structured logging with properties (JournalId, etc.) for observability

**Validation:**
- **Input validation:** Controller accepts CreateJournalRequest DTO (contracts enforce structure via C# types)
- **Domain validation:** Domain entities validate in factories/methods (e.g., Journal.ValidateName checks length)
- **Business validation:** Services check preconditions before delegating (e.g., TransactionService checks journal exists)

**Authentication:** 
- Not implemented; app is self-hosted, assumes single trusted user
- No authorization logic; all endpoints accessible

**Auditing:**
- CreatedAt timestamps on entities (Journal, Transaction)
- Change history implicit in database (no soft deletes or audit table)

---

*Architecture analysis: 2026-08-19*
