---
phase: 01
slug: production-readiness
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-08-21
---

# Phase 01 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| process environment → API host builder | The secret-bearing connection string crosses here as `ConnectionStrings__okozukai`. Whoever can set the process environment (or read it back via `docker inspect`) controls and can read the database credential. | Database credential (secret) |
| container network → Kestrel HTTP listener | Unauthenticated `/health`, `/alive` and `/api/*` become reachable in Production for the first time. Plain HTTP; there is no TLS terminator anywhere in this milestone's topology (D-07). | Health status words; ledger API payloads |
| API process → PostgreSQL | Migrations execute automatically at every startup, mutating the physical schema of the ledger database with whatever privileges the app role holds (including `CREATEDB` for the `3D000` fallback). | DDL / full ledger data |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-01-01 | Information Disclosure | `ConnectionStrings__okozukai` process environment variable | medium | accept | Documented trade-off for a single-tenant, Tailnet-only box (D-13). Discrete `POSTGRES_*` variables and the Docker-secret `_FILE` pattern were considered and rejected as disproportionate. See Accepted Risks R-01. | closed |
| T-01-02 | Information Disclosure | `/health` and `/alive`, mapped outside Development (`Extensions.cs:109-121` `MapDefaultEndpoints`) | low | transfer | Transferred to the network layer per D-11: Phase 3's ACC-03 never publishes the API port to the host, so the endpoints exist only on the internal container network. Verified: `MapHealthChecks` uses the default response writer (status word only, no exception text). **Forward constraint for Phase 3:** if ACC-03 slips, this becomes medium and needs `RequireHost` or auth. | closed |
| T-01-03 | Information Disclosure | D-14 fail-fast guard in `src/Okozukai.Api/Program.cs:24-29` — startup error message and log | high | mitigate | **Verified.** The guard's message is composed from two plain string literals and names only the *key* `ConnectionStrings__okozukai`, never the value. No interpolation (`$"…"`), no `connectionString` reference inside the throw. | closed |
| T-01-04 | Spoofing | Forwarded-header trust (`X-Forwarded-Proto` and friends) | medium | mitigate | **Verified by omission.** Repo-wide grep over `src/` for `ForwardedHeaders`, `X-Forwarded`, `UseHttpsRedirection`, `UseHsts` returns zero hits — no forwarded-header middleware exists, so there is no header to forge trust from (D-07). **Forward constraint for Phase 2/3:** if a TLS terminator is introduced, `KnownProxies`/`KnownNetworks` must be configured explicitly. | closed |
| T-01-05 | Denial of Service (self-inflicted) | Migration retry loop, `MigrationExtensions.cs:18-58` | low | mitigate | **Verified.** The guard at `Program.cs:24-29` throws during host-builder configuration, ahead of `app.Services.ApplyDatabaseMigrations()` at `Program.cs:47`. A missing connection string can no longer reach the ten-round / 3-second backoff loop. | closed |
| T-01-06 | Tampering / Elevation of Privilege | Unconditional `ApplyDatabaseMigrations()` against the production database, incl. the `3D000` auto-create path | medium | accept | Accepted per D-01/D-02/D-03: single instance is a locked milestone constraint, migration failure crashes the process rather than serving a half-migrated ledger, and the auto-create fallback is a first-boot safety net. See Accepted Risks R-02. | closed |
| T-01-SC | Tampering | Package-manager installs (NuGet) | low | accept | **Verified.** `git diff --name-only 31718aa~1..b566bec` lists no `.csproj` — the phase added zero package references. RESEARCH.md's Package Legitimacy Audit records "Not applicable this phase"; D-10 forbids adding a health-check package because `AddNpgsqlDbContext` already registers one. See Accepted Risks R-03. | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-01 | T-01-01 | Single-tenant, Tailnet-only host: the database password is visible to anyone who can run `docker inspect` or read `/proc`. Discrete `POSTGRES_*` vars and the Docker-secret `_FILE` pattern were rejected as disproportionate (D-13). Revisit if the box ever becomes multi-tenant. | archer0302 (D-13) | 2026-08-21 |
| R-02 | T-01-06 | Automatic migration at every startup against the production database, with a `CREATEDB`-dependent auto-create fallback (D-01/D-02/D-03). The app role needs `CREATEDB` for the fallback — **carried forward to Phase 2** as a Postgres provisioning note; without it the fallback logs a creation failure and the boot crash-loops. | archer0302 (D-01/D-02/D-03) | 2026-08-21 |
| R-03 | T-01-SC | No package installs this phase, so no supply-chain surface was added. Re-run the Package Legitimacy Gate if any revision introduces an install. | archer0302 (D-10) | 2026-08-21 |

*Accepted risks do not resurface in future audit runs.*

---

## Deferred Verification (carried to Phase 2)

Recorded in `01-UAT.md` test 2 as human-accepted deferrals — not open threats, but deploy-time checks that Phase 1 did not exercise:

- An interrupted mid-migration startup should resume cleanly from `__EFMigrationsHistory` on the next boot rather than leaving a half-applied schema.
- A connection-string password containing `;` or `=` must be quoted per Npgsql's keyword-value rules. `.env.example` documents only the plain unquoted form; the real credential is authored at deploy time.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-21 | 7 | 7 | 0 | /gsd-secure-phase (verify:post hook, ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-21
