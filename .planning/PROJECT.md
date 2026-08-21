# Okozukai

## What This Is

Okozukai (お小遣い — "pocket money") is a self-hosted personal budget tracker. Journals are independent budget contexts, each with its own currency; income and expense transactions are recorded against a journal, tagged with colour-coded labels, and rolled up into spending breakdowns and monthly charts. Closed journals become read-only archives.

It is built for one user — its author — running on hardware they control. It is not a product, has no other users, and is not monetised.

## Core Value

The recorded ledger is accurate and never lost — every transaction sums correctly, in the right currency, and survives.

## Requirements

### Validated

<!-- Shipped and confirmed working. Inferred from the codebase map and covered by tests. -->

- ✓ User can create journals, each with a single currency — existing
- ✓ User can close a journal to make it read-only, and reopen it — existing
- ✓ User can delete a journal only when closed, cascading its transactions — existing
- ✓ User can record income and expense transactions with amount, date, and note — existing
- ✓ User can edit and delete their own transactions — existing
- ✓ User can create colour-coded tags and attach them to transactions — existing
- ✓ User can filter transactions by date range, tag, and note text — existing
- ✓ User can see a balance summary (total in, total out, net) per journal — existing
- ✓ User can see transactions grouped by year and month with rollups — existing
- ✓ User can see spending broken down by tag, and per-tag by month — existing
- ✓ User can see monthly income vs expenses and a net balance trend — existing
- ✓ User can toggle individual dashboard chart panels, persisted per journal — existing
- ✓ User can export a journal's transactions to CSV — existing
- ✓ User can use the app in dark mode — existing
- ✓ Database schema is created automatically when deployed in Production — Phase 1
- ✓ API boots correctly under `ASPNETCORE_ENVIRONMENT=Production` — no HTTPS-redirect loop, dev seed data stays dev-only, `/health` and `/alive` reachable — Phase 1
- ✓ Missing deployment configuration fails fast at startup, naming the key and never echoing the secret — Phase 1

### Active

<!-- Milestone 1: homelab deployment. Hypotheses until shipped. -->

- [ ] Okozukai runs on the homelab Linux box as a container stack, serving from its own hardware
- [ ] Frontend is served as a production build, not a Vite dev server
- [ ] Frontend and API are reachable on a single origin, removing CORS and the build-time API URL
- [ ] Deployment configuration and secrets come from environment, not developer-machine user secrets
- [ ] The app is reachable over the Tailnet and not otherwise exposed
- [ ] Transaction queries are indexed on `(JournalId, OccurredAt)` and the tag join keys
- [ ] `DevSeedData.SeedDevelopmentData` must not be able to wipe a real database — it calls `RemoveRange` on all tags and journals guarded only by "zero transactions", so a journals-and-tags-but-no-transactions database would be destroyed by an accidental Development start (emerged in Phase 1; not a Phase 1 blocker)

### Out of Scope

<!-- Explicit boundaries for this milestone. -->

- **Authentication** — Tailnet isolation is the security model for now. Revisit if the app ever leaves the tailnet. Note this leaves the export-by-journal-GUID hole open.
- **Backups** — Deferred to a later milestone. Accepted risk: once real data lives on the homelab it exists in exactly one place until this is addressed.
- **Frontend test coverage and keyboard accessibility** — Real gaps (2 spec files for a multi-component SPA; tag pills unreachable by keyboard) but they do not move deployment forward.
- **Rewriting the backend in TypeScript** — Considered and rejected. ~3,100 lines for feature parity, and C#'s `decimal` gives exact money arithmetic that JavaScript's `number` cannot.
- **In-memory grouping performance work** — `GetSpendingByTag` loads all matching transactions into memory. Real, but indexes address the nearer-term cost.
- **Grafana** — Removed. Its three panels duplicated in-app Chart.js charts and it was never part of the observability path.

## Context

**Origin.** Built in a roughly five-day burst 19–23 February 2026, then committed as a single `init` commit on 28 February. Git history is therefore not a useful guide to how it was built — the real sequence survives only in the EF migration filenames. Grafana was added 3 March, after which the project lay dormant for about five and a half months.

**The Phase 5 pivot.** `20260222120000_AddJournalsPhase5` replaced flat multi-currency transactions with journals, and removed Exchange transactions. One journal equals one currency; balances never merge across currencies and there is no implicit conversion. Several downstream invariants depend on this.

**Current development environment.** Local PostgreSQL 17 via Homebrew with all six migrations applied. Development previously ran against a hosted Supabase instance, now retired — that data is abandoned, so the local database and the future homelab database both start empty. No Docker is required for local development.

**Deployment gap.** A nine-item analysis against commit `13cbdb6` identified what stands between `aspire run` locally and a container stack on the homelab. Four items are hard blockers (migrations gated to Development, no production frontend build, no container images or compose, secrets in user secrets); the rest concern correctness and safety in production.

**Test posture.** 31 unit + 22 integration + 16 frontend component + 14 Playwright E2E, all passing. The E2E suite was unrunnable until August 2026 — `playwright-core` had installed without its `server/firefox` directory, and four tests carried selector and sequencing defects.

**Known limitations carried forward.** Tag uniqueness is checked before saving so concurrent writes can race; period rollups report `opening` as 0 rather than cumulative; `Tag.Color` has no domain-level format validation.

## Constraints

- **Tech stack**: .NET 10 clean architecture (Api → Application → Domain → Infrastructure), Vue 3 + TypeScript SPA, PostgreSQL, .NET Aspire orchestration — Established and working; no appetite to change it.
- **Money handling**: Amounts are `decimal` with `HasPrecision(18, 2)` — Exact base-10 arithmetic is non-negotiable for a ledger. This rules out runtimes without a native decimal type.
- **Currency**: One journal, one currency — Set by the Phase 5 pivot; balances must never merge across currencies.
- **Deployment target**: A single Linux box with Docker — Not Kubernetes, not a hypervisor, not a NAS appliance. Single-instance assumptions are acceptable.
- **Access**: Reachable over Tailscale only — Network isolation substitutes for authentication in this milestone.
- **Layering**: Only `Okozukai.Infrastructure` touches EF Core; domain entities carry no persistence concerns — Enforced by project references; keeps the domain testable without a database.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Treat Okozukai as an ongoing project, with homelab deployment as milestone 1 | The repo has been active since February and will continue past deployment; a deployment-only project would close prematurely | — Pending |
| Tailnet-only access rather than authentication | Single user, single trusted network; zero application code, and the existing `TAILNET_IP` plumbing already anticipates it | — Pending |
| Defer backups to a later milestone | Getting it running comes first; accepted risk is a window where the ledger exists in exactly one place | ⚠️ Revisit — this is the highest-consequence deferral in the milestone |
| Include DB indexes in this milestone | The schema is being touched during deployment anyway, and the cost of missing indexes grows silently with data volume | — Pending |
| Removed Grafana | Its three panels duplicated in-app charts; it queried PostgreSQL directly and was never part of the observability path | ✓ Good — removed in `4ec4ca6`, nothing lost |
| Consolidated AI instructions onto root `AGENTS.md` | `.github/copilot-instructions.md` was invisible to Claude Code, the tool actually in use; `AGENTS.md` is read by Claude Code, Copilot, Codex, and Cursor alike | ✓ Good — also corrected three factual drifts against live code |
| Adopted `AddViteApp` over `AddNpmApp` | `Aspire.Hosting.NodeJs` is a dead-end package stopping at 9.5.2; `Aspire.Hosting.JavaScript` is actively released and exposes `PublishAsStaticWebsite` for same-origin deployment | — Pending |
| Kept the C# backend rather than rewriting in TypeScript | ~3,100 lines for exact feature parity, and JavaScript has no native decimal type — a correctness regression for a ledger | ✓ Good |
| Stopped tracking `.claude/` | 680 files and 11MB of installer-generated tooling, reinstallable via npx; `.planning/` remains tracked as real project knowledge | ✓ Good |
| Deleted `UseHttpsRedirection()` outright rather than adding `UseForwardedHeaders` | This topology has no TLS terminator anywhere in the milestone (D-07), so there is no forwarded-proto header to trust; removal is the correct fix and leaves no header to forge trust from | ✓ Good — Phase 1, also closes threat T-01-04 by omission |
| Fixed the `AddJournalsPhase5` migration in place | It unconditionally inserted a `Default` journal into *every* fresh database, violating PROD-02's zero-seed guarantee; the migration has never run against a real production database, so editing `Up()` is safe and this is the last moment it will be | ✓ Good — Phase 1, caught by boot verification against a genuinely empty database |
| Accepted the DB password in a single `ConnectionStrings__okozukai` env var | Single-tenant Tailnet-only box; discrete `POSTGRES_*` vars and the Docker-secret `_FILE` pattern were both considered and rejected as disproportionate (D-13) | ⚠️ Revisit if the box ever becomes multi-tenant — recorded as accepted risk R-01 in `01-SECURITY.md` |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-08-21 after Phase 1*
