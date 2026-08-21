# Roadmap: Okozukai — Milestone 1 (Homelab Deployment)

## Overview

Okozukai works today, but only under `dotnet run` on a developer machine with
`ASPNETCORE_ENVIRONMENT=Development`. This milestone takes it from that state to a
container stack running unattended on a homelab Linux box, reachable only over Tailscale.
The path runs through four phases: first make the app itself correct when hosted in
Production (migrations, seed data, proxy headers, health checks, config) — the hardest
blocker, since nothing built on top of a broken Production mode is worth deploying. Then
package the frontend and API into containers that serve from a single origin, so the
browser never talks to two hosts and the deployed bundle carries no baked-in URL. Then
wire that stack to the Tailnet and lock down everything else. Query indexes are schema-only
and independent of the deployment mechanics, so they stand as their own phase and can land
in any order relative to the others.

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Production Readiness** - The app behaves correctly when hosted in Production, not just Development (completed 2026-08-21)
- [ ] **Phase 2: Single-Origin Packaging** - Frontend and API build into containers that serve from one origin via one command
- [ ] **Phase 3: Tailnet Access & Network Isolation** - The stack is reachable over Tailscale only, with the database and internals unexposed
- [ ] **Phase 4: Query Indexes** - Transaction and tag queries run against indexes instead of full table scans

## Phase Details

### Phase 1: Production Readiness

**Goal**: Okozukai behaves correctly when started with `ASPNETCORE_ENVIRONMENT=Production` — the hosting mode it will actually run in on the homelab — instead of only working under Development.
**Depends on**: Nothing (first phase)
**Requirements**: PROD-01, PROD-02, PROD-03, PROD-04, PROD-05
**Success Criteria** (what must be TRUE):

  1. Starting the API in Production against a brand-new, empty database results in all migrations applying automatically and a working schema — not a startup failure or an empty database on first query.
  2. No development seed data is inserted when the API runs in Production.
  3. Behind a reverse proxy that terminates TLS and forwards `X-Forwarded-Proto`, the API serves requests without entering a redirect loop.
  4. A health-check endpoint responds when the API is not in Development, on a port intended for internal use rather than public exposure.
  5. The API starts successfully using only environment variables for its configuration, including the database connection string — no `dotnet user-secrets` involved.

**Plans**: 1/1 plans executed

Plans:

- [x] 01-01-PLAN.md — Production boot path end-to-end: unconditional migrations, ungated health endpoints, HTTPS redirect removed, fail-fast connection-string guard, committed `.env.example`

### Phase 2: Single-Origin Packaging

**Goal**: The frontend and API build into container images that a single command brings up as one stack, with the browser talking to exactly one origin instead of two.
**Depends on**: Phase 1
**Requirements**: ORIG-01, ORIG-02, ORIG-03, PKG-01, PKG-02, PKG-03, PKG-04, PKG-05, PKG-06
**Success Criteria** (what must be TRUE):

  1. The built frontend bundle contains no absolute API URL — only relative `/api` calls — so pointing the deployment at a new hostname needs no rebuild.
  2. The API no longer sends a permissive CORS header, because nothing in the deployed topology is cross-origin.
  3. A single command builds and starts the whole stack (database, API, production frontend build) from committed configuration, and a browser hitting the one published origin reaches both the app and `/api` without CORS or a second origin.
  4. Recreating the containers preserves existing PostgreSQL data via a named volume, and required secrets are supplied at runtime rather than committed to the repository.
  5. Static assets are served with cache headers that let the PWA service worker fetch a new bundle after a redeploy, instead of pinning a stale cached version indefinitely.

**Plans**: TBD

### Phase 3: Tailnet Access & Network Isolation

**Goal**: The deployed stack is reachable over Tailscale at a stable address, and nothing else in the stack is exposed beyond the container network.
**Depends on**: Phase 2
**Requirements**: ACC-01, ACC-02, ACC-03
**Success Criteria** (what must be TRUE):

  1. The app is reachable from another device on the Tailnet at a stable address.
  2. The app is not reachable from outside the Tailnet — no public port forwarding, no exposure beyond the Tailscale interface.
  3. Connecting directly to the PostgreSQL port from outside the container network fails; only the API can reach the database.
  4. Of the whole compose stack, only the web entry point publishes a port to the host — API and database are reachable exclusively via the internal container network.

**Plans**: TBD

### Phase 4: Query Indexes

**Goal**: Transaction and tag queries run against indexes instead of full table scans, so query cost stops growing linearly with data volume.
**Depends on**: Nothing (schema-only, independent of the deployment phases)
**Requirements**: PERF-01, PERF-02
**Success Criteria** (what must be TRUE):

  1. `EXPLAIN ANALYZE` on a transaction query filtered by journal and date range shows an index scan on `(JournalId, OccurredAt)`, not a sequential scan.
  2. `EXPLAIN ANALYZE` on tag-filtered and tag-grouped transaction queries shows the `TransactionTags` join keys served by an index.
  3. A new EF Core migration exists that creates these indexes and applies cleanly on top of the existing six migrations.

**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 (Phase 4 has no dependency on 1-3 and could run earlier if preferred, but is listed last for narrative clarity)

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Production Readiness | 1/1 | Complete    | 2026-08-21 |
| 2. Single-Origin Packaging | 0/TBD | Not started | - |
| 3. Tailnet Access & Network Isolation | 0/TBD | Not started | - |
| 4. Query Indexes | 0/TBD | Not started | - |
