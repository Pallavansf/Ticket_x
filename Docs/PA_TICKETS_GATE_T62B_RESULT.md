# PA_TICKETS Gate T62B Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** existing PostgreSQL schema baseline and adoption review  
**Target:** `localhost:5432 / pa_tickets_e2e_verify / postgres`  
**Database/provider activity:** read-only catalog queries only

## Executive result

The existing catalog broadly matches the checked-in foundational PostgreSQL
schema family, including the core ticket, workflow, approval, SLA, RBAC
foundation, audit, and waiting-reason objects. However, provenance, exact
definitions, migration history, and later application dependencies are not
reconciled. The database has no migration ledger and no reporting tables or
later RBAC scope/delegation/proposal objects. Adoption or upgrade cannot be
approved from current evidence.

**Recommendation: PARTIAL — source provenance and definition differences remain
unresolved.**

## Evidence-backed matrix

See [T62B_EXISTING_SCHEMA_BASELINE.md](reconciliation/T62B_EXISTING_SCHEMA_BASELINE.md)
for the object inventory and source-to-catalog matrix. The catalog query found
36 public tables and 35 sequences, all owned by `postgres`, including
`PA_TICKETS`, `PA_TICKETS_WORK_ITEM`, and `PA_TICKETS_WAITING_REASON`.

The reviewed source files contain the foundational object family, while
PostgreSQL migrations 028-036 define later additive RBAC/profile/ledger
objects. The catalog lacks `PA_TICKETS_SCHEMA_MIGRATION`, all three reporting
tables, and the later scope/delegation/proposal objects. This is a catalog fact,
not proof that a particular migration was or was not run.

## Code-to-schema assessment

`TicketsDbContext` maps the core catalog objects and waiting-reason fields;
repositories and authorization readers depend on those names. Reporting and
execution code depends on objects introduced by migration 026, which are not
present in the observed catalog. Later authorization readers depend on scope,
delegation, and metadata objects that are not present either. EF/provider
translation, null/default semantics, sequence ownership/current values,
constraints, and transaction behavior remain unverified.

## Adoption and upgrade decision

No destructive change is justified. A safe next step requires an approved
read-only definition comparison (columns, types, nullability, defaults,
constraints, indexes, sequences, and ownership) against a named foundational
baseline, followed by a formal adoption or non-destructive upgrade decision.
Migration 026 remains separately gated; this review neither approved nor
executed it.

## Superuser implications

The approved account is `postgres` and is a superuser. Metadata confirms the
required database and schema privileges, but this is broader than least
privilege. A future schema operation should use an owner-approved dedicated
non-superuser role with narrowly scoped permissions. No account or privilege
was changed here.

## Commands and outcomes

- `git branch --show-current`; `git status --short --ignored`.
- Read-only `rg`/`Get-Content` inventory of PostgreSQL schema scripts,
  migrations, EF mappings, repositories, raw SQL, and prior gate results.
- Guarded `psql` catalog inventory: identity, public tables/sequences, column
  counts, and presence checks for ledger/reporting objects. All queries were
  read-only and targeted only `pa_tickets_e2e_verify` as `postgres`.
- No DDL, DML, migration, schema script, seed, cleanup, privilege change,
  provider-backed test, or protected-target access occurred.

## Preservation

The branch remained `workstream-i-lifecycle-authority`; all modified,
untracked, and ignored work was preserved. No reset, clean, stash, branch
switch, commit, push, or application/migration edit was performed. The two
documentation files created by this gate are the only additions.

**GATE T62B PARTIAL — SCHEMA PROVENANCE OR DIFFERENCES UNRESOLVED**
