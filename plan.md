# Okozukai Project Plan

## Problem statement
Build Okozukai as a web-based personal budget application (C# backend + Vue 3 frontend) from ideation to a usable product, starting with an MVP focused on core transaction tracking and expanding into tags, multi-currency, period rollups, and visualization.

## Current status
**Phases 0–6 complete.**

**Test counts: 31 unit + 20 integration + 16 frontend = 67 total (all passing)**

## Phase 0 - Foundation and project bootstrap ✅
- .NET 10 solution with clean architecture (Api, Application, Domain, Infrastructure)
- .NET Aspire AppHost for orchestration (PostgreSQL + API + Frontend)
- EF Core with PostgreSQL, code-first migrations, resilient startup
- xUnit test suite (unit + integration)

## Phase 1 - MVP: core transaction tracking ✅
- Full CRUD for transactions via REST API
- Vue 3 SPA frontend (`Okozukai.Frontend`) with Tailwind CSS v4
- Transaction list with date/type/amount/currency/note
- Balance summary per currency on dashboard
- Component tests (Vitest) and E2E tests (Playwright)

## Phase 2 - Tags and spending categorization ✅
- `Tag` entity with many-to-many relation to `Transaction`
- Tag CRUD API + color support with random assignment
- Multi-select tagging in transaction form (color-coded pills)
- Tag filter (autocomplete combobox with arrow key navigation)
- Spending by tag section with bar charts

## Phase 3 - Multi-currency and currency exchange ✅
- Exchange transaction type (being removed in Phase 5)
- Per-currency balance tracking

## Phase 4 - Period grouping and rollups ✅
- Year/month grouped transaction views with collapsible sections
- Period rollup summaries (net change per currency) shown inline
- Default month folding, fold-all-months toggle
- Description/note search filter, Manage Tags modal, dark mode

## Phase 5 - Journals and budget contexts ✅
- `Journal` entity (Id, Name, PrimaryCurrency, IsClosed, CreatedAt) with Close/Reopen/Update
- `Transaction` refactored: JournalId FK, CreatedAt, removed currency/exchange fields
- `TransactionType` simplified: only `In` and `Out`
- Full journal CRUD API + close/reopen/delete (delete requires IsClosed=true, cascade-deletes transactions)
- All transaction endpoints scoped to `?journalId=`
- Frontend: journal selector dropdown in header (persists to localStorage), journal CRUD modal
- Frontend: closed-journal banner, add/edit/delete disabled when journal is closed
- Tags remain global (shared across all journals)
- Data migration: auto-assigns existing transactions to a "Default" journal using most-common currency
- Post-phase fixes: tag autocomplete blur, transaction sort order (OccurredAt desc → CreatedAt desc)

## Phase 6 - Customizable Dashboard ✅
- Vue Router added for page navigation (`/` = Transactions, `/dashboard` = Dashboard)
- Navigation tabs in header (Transactions | Dashboard), styled with active indicator
- Chart.js + vue-chartjs integration for data visualization
- **Monthly Income vs Expenses** bar chart (paired green/red bars per month)
- **Spending by Tag** doughnut chart (colored by tag, with percentage tooltips)
- **Net Balance Trend** line chart (cumulative net balance over months)
- **Monthly Spending by Tag** stacked bar chart (tag composition per month)
- New backend API: `GET /api/transactions/spending-by-tag-monthly` for per-month tag breakdown
- Date range presets (This Month, Last 3/6 Months, This Year, All Time)
- Dashboard customization: Customize panel to toggle individual chart visibility
- Chart preferences persisted to `localStorage` per journal
- All charts support dark mode with adaptive colors
- Summary KPI cards (Balance, Total In, Total Out) shown on dashboard
- 2 new backend unit tests, 7 new frontend component tests

## Phase 7 - (Next phase — to be planned)

*To be defined. Candidate features:*
- Spending insights / monthly top-5 spending tags
- Running balance / opening+closing balance per period (currently always 0)
- Multi-journal summary view
- Budget goals / spending limits per tag



## Known issues (from prior code review)
- Tag creation/update has a race condition between uniqueness check and save (low risk for single-user app)
- `TransactionPeriodRollupResponse.Opening`/`Closing` are always 0/net (not cumulative across periods)
- `Tag.Color` has no domain-level format validation

## Cross-phase architecture decisions
- Keep domain logic in `Domain` and avoid business rules inside controllers
- Use DTO mapping at API boundaries to prevent domain leakage
- Prefer explicit transaction boundaries for write operations
- Preserve auditability: avoid destructive data rewrites without traceability
- One journal = one currency; no implicit currency conversion
