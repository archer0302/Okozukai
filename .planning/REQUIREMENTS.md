# Requirements: Okozukai — Milestone 1 (Homelab Deployment)

**Defined:** 2026-08-20
**Core Value:** The recorded ledger is accurate and never lost — every transaction sums correctly, in the right currency, and survives.

## v1 Requirements

Requirements for milestone 1. Each maps to a roadmap phase. Derived from a nine-item
deployment gap analysis performed against commit `13cbdb6`, with file and line evidence
read from the working tree.

### Production Readiness

The app currently only behaves correctly under `ASPNETCORE_ENVIRONMENT=Development`.
These make it survive Production mode.

- [x] **PROD-01**: Database schema is created and migrated automatically when the API starts in Production, so a fresh deployment comes up with a working schema rather than failing on every query
- [x] **PROD-02**: Development-only seed data is never inserted outside Development
- [x] **PROD-03**: The API honours `X-Forwarded-Proto` from a TLS-terminating proxy, so requests are not redirected into a loop
- [x] **PROD-04**: Health endpoints respond outside Development on an internal port, so the container runtime can gate startup and detect failure
- [x] **PROD-05**: All deployment configuration, including the database connection string, is read from environment variables rather than developer-machine user secrets

### Single Origin

The browser currently talks to two origins, which forces CORS open and compiles the API
URL into the frontend bundle.

- [ ] **ORIG-01**: The frontend calls the API on a relative path, so the deployed bundle carries no absolute API URL and a hostname change needs no rebuild
- [ ] **ORIG-02**: The API no longer enables a permissive CORS policy, because nothing is cross-origin
- [ ] **ORIG-03**: A single origin serves both the built frontend and the API, routing `/api` to the backend and everything else to static files

### Packaging

There is currently no artifact to deploy.

- [ ] **PKG-01**: The API builds into a container image from a repeatable multi-stage build
- [ ] **PKG-02**: The frontend builds to production static assets rather than running a Vite dev server
- [ ] **PKG-03**: A single command brings up the whole stack — database, API, and web — on the homelab box
- [ ] **PKG-04**: PostgreSQL data survives container recreation via a named volume
- [ ] **PKG-05**: Secrets are supplied to the stack without being committed to the repository
- [ ] **PKG-06**: Static assets are served with cache headers that let the PWA service worker pick up new deployments rather than pinning a stale bundle

### Access

- [ ] **ACC-01**: The app is reachable from the Tailnet at a stable address
- [ ] **ACC-02**: PostgreSQL is not reachable from outside the container network
- [ ] **ACC-03**: Only the web entry point publishes a port to the host

### Query Performance

- [ ] **PERF-01**: Transaction queries filtered by journal and date range are served by an index on `(JournalId, OccurredAt)` rather than a full table scan
- [ ] **PERF-02**: Tag-filtered and tag-grouped queries are served by indexes on the `TransactionTags` join keys

## v2 Requirements

Acknowledged but deferred. Not in this roadmap.

### Durability

- **BACK-01**: Database is dumped nightly to storage off the homelab box
- **BACK-02**: A documented restore procedure exists and has been tested at least once

### Security

- **SEC-01**: Requests are authenticated before reaching application endpoints
- **SEC-02**: Journals are scoped to an owner, so export cannot be driven by guessing a GUID
- **SEC-03**: Rate limiting protects the API from runaway clients

### Quality

- **QUAL-01**: Vue components have unit test coverage proportionate to the SPA's size
- **QUAL-02**: Tag selection controls are reachable and operable by keyboard
- **QUAL-03**: Spending aggregation is performed in the database rather than in memory

## Out of Scope

Explicitly excluded from this milestone. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Authentication | Tailnet isolation is the security model for this milestone. Leaves export-by-GUID open; revisit if the app ever leaves the tailnet. |
| Backups | Deferred to v2. Accepted risk: real data will exist in exactly one place until addressed. |
| Kubernetes or a hypervisor deployment | Target is a single Linux box with Docker; single-instance assumptions are acceptable. |
| Multi-user support | The app has exactly one user and no ownership model. |
| Horizontal scaling | Migrations run on startup, which assumes a single API instance. |
| Rewriting the backend in TypeScript | ~3,100 lines for feature parity, and JavaScript has no native decimal type — a correctness regression for a ledger. |
| CI/CD pipeline | Deploy is manual for now; automating it before it works once would be premature. |
| In-memory grouping rewrite | Real bottleneck, but indexes address the nearer-term cost. Tracked as QUAL-03. |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PROD-01 | Phase 1 | Complete |
| PROD-02 | Phase 1 | Complete |
| PROD-03 | Phase 1 | Complete |
| PROD-04 | Phase 1 | Complete |
| PROD-05 | Phase 1 | Complete |
| ORIG-01 | Phase 2 | Pending |
| ORIG-02 | Phase 2 | Pending |
| ORIG-03 | Phase 2 | Pending |
| PKG-01 | Phase 2 | Pending |
| PKG-02 | Phase 2 | Pending |
| PKG-03 | Phase 2 | Pending |
| PKG-04 | Phase 2 | Pending |
| PKG-05 | Phase 2 | Pending |
| PKG-06 | Phase 2 | Pending |
| ACC-01 | Phase 3 | Pending |
| ACC-02 | Phase 3 | Pending |
| ACC-03 | Phase 3 | Pending |
| PERF-01 | Phase 4 | Pending |
| PERF-02 | Phase 4 | Pending |

**Coverage:**

- v1 requirements: 19 total
- Mapped to phases: 19 ✓
- Unmapped: 0 ✓

## Definition of Done

Milestone 1 is complete when:

1. Okozukai serves from the homelab Linux box, not a developer machine
2. The stack comes up from a single command against an empty database and reaches a working schema
3. The app is usable end to end over the Tailnet — create a journal, record a transaction, see it in the charts
4. Nothing but the web entry point is reachable from outside the container network
5. Transaction and tag queries run against indexes rather than full scans

---
*Requirements defined: 2026-08-20*
*Last updated: 2026-08-20 after roadmap creation — traceability mapped to phases 1-4, 19/19 requirements covered*
