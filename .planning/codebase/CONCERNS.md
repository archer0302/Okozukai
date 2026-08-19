# Codebase Concerns

**Analysis Date:** 2026-08-19

## Security Vulnerabilities

**CORS Configuration - Open to All Origins:**
- Issue: `Program.cs` line 17 configures CORS with `AllowAnyOrigin()`, allowing requests from any domain
- Files: `src/Okozukai.Api/Program.cs` (lines 13-21)
- Impact: Any website can make cross-origin requests to your API, potentially exposing data to unauthorized clients
- Fix approach: Replace `AllowAnyOrigin()` with a whitelist of allowed origins. Example: `policy.WithOrigins("https://yourdomain.com")` for production, or at minimum validate against environment-specific origins

**No Authentication/Authorization:**
- Issue: API endpoints have no auth checks. `TransactionsController`, `JournalsController`, `TagsController` accept requests from anyone
- Files: `src/Okozukai.Api/Controllers/*.cs` (all controller files), `src/Okozukai.Api/Program.cs` (line 55)
- Impact: Any client can read, create, update, or delete any user's financial data. For a self-hosted app, this is acceptable if network access is restricted, but should be documented clearly
- Fix approach: Implement bearer token auth via OpenID Connect / JWT, or document network isolation requirements prominently

**Unrestricted Data Export:**
- Issue: Export endpoint doesn't validate journal ownership; any journal ID can be exported by any caller
- Files: `src/Okozukai.Api/Controllers/TransactionsController.cs` (lines 93-104), `src/Okozukai.Application/Transactions/TransactionService.cs` (lines 209-252)
- Impact: Users can export any journal's transaction history if they guess a journal GUID
- Fix approach: Add journal ownership validation before exporting; implement user/tenant isolation at the API boundary

## Performance Bottlenecks

**Tag Color Assignment Scales Poorly:**
- Problem: Creating a tag calls `GetAllAsync()` to count existing tags, then uses modulo to assign a color
- Files: `src/Okozukai.Application/Transactions/TagService.cs` (line 55)
- Cause: For every tag creation, all tags must be fetched into memory; with thousands of tags, this becomes O(n)
- Improvement path: Store a color counter in the database or use a deterministic hash-based color assignment instead of position-based

**In-Memory Grouping for Large Datasets:**
- Problem: `GetSpendingByTag()` and `GetSpendingByTagMonthly()` call `GetForGroupingAsync()` which loads ALL matching transactions into memory, then groups in LINQ
- Files: `src/Okozukai.Application/Transactions/TransactionService.cs` (lines 106-207)
- Cause: No pagination; journals with years of history could load millions of records at once
- Improvement path: Perform grouping/aggregation in the database query, not in-memory. Use SQL GROUP BY / window functions

**Missing Database Indexes:**
- Problem: No indexes specified for common query patterns (date ranges, journalId filtering, tag filtering)
- Files: `src/Okozukai.Infrastructure/Persistence/Configurations/*.cs` (TransactionConfiguration.cs, etc.)
- Cause: Full table scans on large transaction tables for filtered queries
- Improvement path: Add `HasIndex()` fluent API calls in EF Core configurations for `(JournalId, OccurredAt)`, `(JournalId, CreatedAt)`, and join keys

## Test Coverage Gaps

**Minimal Frontend Test Coverage:**
- What's not tested: Most Vue components have no unit tests; only 2 `.spec.ts` files exist for a multi-component SPA
- Files: `src/Okozukai.Frontend/src/components/*.vue`, `src/Okozukai.Frontend/src/pages/*.vue`
- Risk: UI bugs, state management issues, and error handling go undetected until manual testing or production
- Priority: High - frontend is the primary user interface

**No Tests for JournalService or TagService:**
- What's not tested: Journal create/update/delete, journal close/reopen logic, tag creation with color assignment, tag deletion with detachment
- Files: `src/Okozukai.Application/Transactions/JournalService.cs`, `src/Okozukai.Application/Transactions/TagService.cs`
- Risk: Business logic bugs in journal and tag operations may surface only during integration testing or user testing
- Priority: High - these are critical domain operations

**Limited API Integration Test Coverage:**
- What's not tested: `/api/tags/*` endpoints, journal close/reopen/delete endpoints, error cases for concurrent updates
- Files: `tests/Okozukai.IntegrationTests/TransactionsApiTests.cs` (18KB file covers only transaction endpoints)
- Risk: Controller-level bugs, serialization issues, and HTTP status code correctness gaps
- Priority: Medium

**Single E2E Test File:**
- What's not tested: Most user workflows (creating tags, filtering by tag, exporting, journal management)
- Files: `src/Okozukai.Frontend/e2e/dashboard.spec.ts`
- Risk: End-to-end regressions in common scenarios may only be caught by manual testing
- Priority: Medium

## Fragile Areas

**Frontend Error Handling Without Centralized Interceptors:**
- Files: `src/Okozukai.Frontend/src/api/client.ts`, `src/Okozukai.Frontend/src/api/transactionService.ts`
- Why fragile: Axios client has no response/error interceptors; every component must implement its own try/catch. Inconsistent error handling across components
- Safe modification: Add axios interceptors in `client.ts` to centralize error handling (log errors, map HTTP status codes to user messages, retry logic)
- Test coverage: Add interceptor tests and mock axios in component tests

**CSV Export Logic with String Manipulation:**
- Files: `src/Okozukai.Application/Transactions/TransactionService.cs` (lines 229-252)
- Why fragile: Uses string concatenation for CSV generation; `EscapeCsv()` only escapes quotes but doesn't handle line breaks or special characters correctly
- Safe modification: Use a dedicated CSV library (e.g., CsvHelper) instead of manual string building; test with edge cases (notes with commas, quotes, newlines)
- Test coverage: Add unit tests for CSV export with special characters in notes/tags

**Multiple Async-Void Error Handlers in Frontend:**
- Files: `src/Okozukai.Frontend/src/App.vue` (multiple try/catch blocks with only `console.error()`)
- Why fragile: Errors logged to console only; no user-visible toast/alert; race conditions possible if operations complete out of order
- Safe modification: Implement a global error notification system (Toast component); ensure async operations set loading states before and after
- Test coverage: Mock API failures and verify error messages appear to users

## Architectural Concerns

**No Request/Response Logging:**
- Issue: No middleware to log incoming requests and outgoing responses for debugging or audit trail
- Files: `src/Okozukai.Api/Program.cs` (no logging middleware added)
- Impact: Difficult to troubleshoot API issues; no audit trail for data access
- Recommendation: Add a logging middleware that captures method, path, status code, and execution time (exclude health checks)

**No Rate Limiting:**
- Issue: No rate limiting on any endpoint; users can spam create/delete operations
- Files: All controller endpoints are unprotected
- Impact: Potential DoS vulnerability; no protection against abuse
- Recommendation: Add rate limiting middleware (e.g., using `AspNetCoreRateLimit` NuGet package) with per-IP or per-user quotas

**No Input Validation at Controller Level:**
- Issue: Controllers pass request objects directly to services; validation only happens in domain models
- Files: `src/Okozukai.Api/Controllers/*.cs`
- Impact: Invalid requests reach service layer before failing; inconsistent error response formats for different validation failures
- Recommendation: Add FluentValidation validators for all request DTOs and apply as a validation filter on controllers

**No API Versioning Strategy:**
- Issue: No versioning scheme defined; breaking changes to endpoints would require all clients to update simultaneously
- Files: Entire API surface area
- Impact: Difficult to introduce non-breaking changes; no backwards compatibility path
- Recommendation: Adopt URL versioning (`/api/v1/transactions`) or header-based versioning and document deprecation policy

## Scaling Limits

**In-Memory Transaction Grouping:**
- Current capacity: Tested with ~100-200 transactions per query; untested with 10k+ transactions
- Limit: Loading all transactions into memory for grouping/aggregation breaks with large datasets (memory exhaustion, timeout)
- Scaling path: Move grouping/aggregation to database queries (SQL GROUP BY, window functions); use pagination for large result sets

**No Horizontal Scalability:**
- Current capacity: Single API instance + single PostgreSQL instance
- Limit: Database connection pool exhaustion under high concurrency; no load balancing strategy
- Scaling path: Implement database connection pooling configuration tuning; add API replicas behind a load balancer; consider read replicas for query-heavy operations

## Missing Critical Features

**No Multi-User/Multi-Tenant Support:**
- Problem: Each journal is independently scoped, but no concept of "users" or "accounts"; API exposes all journals to all callers
- Blocks: Sharing budgets between users, restricting access, audit trails per user
- Recommendation: Implement user authentication and tenant isolation; add UserId to all domain entities

**No Soft Delete for Journals:**
- Problem: Deleted journals cannot be recovered; cascading delete removes all associated transactions
- Blocks: Data recovery after accidental deletion; audit trail of deletions
- Recommendation: Implement soft delete (mark as deleted, hide from queries by default) for journals and their transactions

**No Undo/Redo for Transactions:**
- Problem: Once deleted, a transaction is permanently gone; no transaction history or audit log
- Blocks: Users cannot recover accidentally deleted transactions
- Recommendation: Implement event sourcing or maintain a changelog table with all modifications

## Dependencies at Risk

**Tailwind CSS v4 + PWA Configuration:**
- Risk: `vite-plugin-pwa` and PostCSS configuration may conflict with Tailwind v4 which handles its own PostCSS setup; PWA caching strategy not configured
- Impact: Service worker may serve stale assets; dark mode or CSS updates may not propagate to cached builds
- Migration plan: Audit PWA cache invalidation strategy; test dark mode toggle with service worker active; consider removing PWA if not actively used

## Monitoring & Observability Gaps

**OpenTelemetry Configured but Not Deployed:**
- Issue: `ServiceDefaults/Extensions.cs` configures OTEL export only if `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- Impact: Distributed tracing and metrics are lost if no OTEL collector is deployed
- Recommendation: Document required OTEL setup (Jaeger/Grafana Loki) in README; provide docker-compose for local tracing

**Limited Structured Logging:**
- Issue: Service layer logs are mostly informational (`_logger.LogInformation(...)`); no structured fields for filtering
- Files: `src/Okozukai.Application/Transactions/*.cs`
- Impact: Hard to correlate logs across requests; difficult to troubleshoot errors in production
- Recommendation: Add structured logging with correlation IDs; log request context at the middleware level

---

*Concerns audit: 2026-08-19*
