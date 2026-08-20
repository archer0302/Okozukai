---
status: testing
phase: 01-production-readiness
source: [01-VERIFICATION.md]
started: 2026-08-20T23:03:58Z
updated: 2026-08-20T23:03:58Z
---

## Current Test

number: 1
name: Read the captured Production-boot startup log and confirm migration lines are present with no seeding announcement and no HTTPS-redirect or certificate warning
expected: |
  Log contains 'Attempting to apply database migrations' / 'Migrations applied successfully' lines and zero mentions of HTTPS redirect or TLS certificate warnings.
awaiting: user response

## Tests

### 1. Read the captured Production-boot startup log (or re-run one) and confirm it shows the migration attempt/success lines with no environment-name announcement of seeding, and no HTTPS-redirect or certificate warning anywhere in the output.
expected: Log contains 'Attempting to apply database migrations' / 'Migrations applied successfully' lines and zero mentions of HTTPS redirect or TLS certificate warnings.
result: [pending]

### 2. Confirm the acceptable-risk framing for the two `verification: backstop` must-haves: (1) an interrupted mid-migration startup resumes cleanly from __EFMigrationsHistory on next boot rather than leaving a half-applied schema; (2) a connection-string password containing `;` or `=` is handled correctly via Npgsql's quoting rules at real deploy time (not exercised — .env.example only documents the plain unquoted form).
expected: Human agrees these two backstop truths are acceptably deferred to Phase 2 deploy-time verification (real credential authored then) rather than needing a synthetic interruption/quoting test in Phase 1.
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
