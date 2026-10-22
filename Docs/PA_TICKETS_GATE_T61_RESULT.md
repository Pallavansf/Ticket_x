# PA_TICKETS Gate T61 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** read-only SQL-to-code reconciliation  
**Database/provider activity:** none

## Executive result

The repository contains enough checked-in source to identify concrete SQL-to-code discrepancies, but no single live-schema truth is available. `database/schema/001_PA_TICKETS_SCHEMA.sql` is documented as an Oracle baseline (`database/README.md`); PostgreSQL uses separate test/schema scripts and provider-specific migrations. The reconciliation therefore distinguishes source facts from live-state questions. Two review-only, provider-separated proposal artifacts were added for the waiting-reason discrepancy; all statements are commented and unexecuted.

## Source baseline and fingerprints

Reviewed the roadmap and T60 result, `database/README.md`, `database/schema/001_PA_TICKETS_SCHEMA.sql`, ordered migrations, PostgreSQL migration directory, reporting candidates, `tests/scripts` schema variants, `TicketsDbContext`, `ExecutionDbContext`, repositories, raw SQL, provider capabilities, and test guard documentation.

Migration 026 hashes were independently recomputed:

| Artifact | SHA-256 | Interpretation |
|---|---|---|
| `database/migrations/026_phase8_6_reporting.sql` | `791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4` | Original artifact; not approved/applied by this gate |
| `database/migrations/026_phase8_6_reporting.review.v3.sql` | `97EB600B6B7F1D73985A28703CA0337A59F42F14A7EFB7FEA557F468638DAE49` | Review candidate; not approved/applied by this gate |

The SQL baseline is not a live-schema inspection. “Missing” below means **not found in the reviewed SQL file(s)**.

## Object-level reconciliation matrix

| Object/type and provider | SQL definition/source | Application reference | Discrepancy and dependency | Proposed correction | Status |
|---|---|---|---|---|---|
| `PA_TICKETS_WAITING_REASON` table and `PA_TICKETS_WAITING_REASON_SEQ` — Oracle | Not found in `database/schema/001_PA_TICKETS_SCHEMA.sql` or ordered migration 004. | `TicketsDbContext.cs:8,20`; `PersistentWaitingReasonCatalog.cs:8-22,26-37`; EF maps nine fields and queries active/effective rows. | Current Oracle baseline does not document the object required by the application. Reference-data policy and NUMBER(1) conventions are unresolved. | Review-only Oracle proposal: `Docs/reconciliation/proposed/oracle/T61_waiting_reason_reconciliation.sql`. | MISSING / OWNER DECISION REQUIRED |
| `PA_TICKETS_WAITING_REASON.N_REQUIRE_EXPECTED_DATE` column — PostgreSQL | `tests/scripts/postgresql-schema.sql:61-72` and `tests/scripts/postgresql-foundational-ddl.sql:74-85` define the table but omit this column. | `TicketsDbContext.cs:20`; `PersistentWaitingReasonCatalog.cs:22,35`. | Confirmed source discrepancy in both reviewed PostgreSQL baselines. Existing rows need a deliberate default/backfill decision. | Review-only PostgreSQL proposal: `Docs/reconciliation/proposed/postgresql/T61_waiting_reason_reconciliation.sql`. | OUTDATED |
| `PA_TICKETS_REPORT_EXPORT`, `PA_TICKETS_REPORT_SCHEDULE`, `PA_TICKETS_REPORT_SCHEDULE_EXECUTION` — PostgreSQL/Oracle | Reporting objects are introduced by migration 026 originals/candidates, not the Oracle baseline. Candidate v3 includes strict preflight and provider-specific PostgreSQL DDL; no Oracle equivalent was found in reviewed 026 files. | `ExecutionDbContext.cs:10-13`; `ReportExportService.cs`; `ReportScheduleService.cs`. | Reporting code cannot be reconciled to the baseline without migration-026 approval, schema fingerprint, ownership/FK decisions, and provider-specific definitions. | Do not create another candidate. Keep migration-026 contracts and owner decisions separate. | UNVERIFIED / OWNER DECISION REQUIRED |
| Core ticket/work-item/attachment/audit tables — Oracle | Present in `database/schema/001_PA_TICKETS_SCHEMA.sql` with keys, FKs, indexes, and current-version columns. | `TicketsDbContext.cs:18-25`; `AttachmentService.cs`; `EfTicketRepository.cs`. | Names broadly align, but exact nullability, defaults, sequence ownership, row-version behavior, and current-version enforcement require column-level comparison and provider verification. | No automatic correction justified. | UNVERIFIED |
| RBAC users, roles, permissions, memberships, scopes — PostgreSQL/Oracle | Oracle baseline contains foundational organization/RBAC tables; PostgreSQL scripts and migrations 030-038 define separate evolution. | `DatabaseAuthorizationFactsReader.cs`; `DatabaseDelegationFactsReader.cs`; `DatabaseAuthorizationService.cs`. | Historical migration ordering, active/effective predicates, IDs, and audit-actor compatibility differ by script family. Live presence and data state are unknown. | Reconcile migrations 030-038 against the selected provider baseline under owner review; no DDL generated here. | CONFLICT / OWNER DECISION REQUIRED |
| Sequence/default strategy — both providers | Oracle baseline and ordered SQL use many Oracle sequence names; PostgreSQL scripts use PostgreSQL sequences/defaults. | `DatabaseProviderCapabilities`; both DbContexts use `SequenceDefault(...)`; identity generators use provider-specific SQL (`OracleTicketServices.cs`). | Sequence ownership, current values, and identity/generated-column semantics cannot be proven statically. Reset/advance is forbidden here. | Provider-specific metadata checks and baseline evidence before any write. | UNVERIFIED |
| Audit chain lock and audit/event rows — both providers | Baseline and scripts define audit/event/lock tables, but definitions vary across schema generations. | `AuditChainCoordinator`; `TicketsDbContext.cs:21-22`; `ExecutionDbContext.cs:11-13`. | Atomicity, lock behavior, hash serialization, FK actor validity, and rollback are runtime properties. | No change proposed. Require provider fault/concurrency evidence. | UNVERIFIED |
| Workflow/approval/SLA execution objects — both providers | Baseline contains many objects; execution context maps additional columns, row-version tokens, and provider conversions. | `ExecutionDbContext.cs:10-13`; workflow/approval/SLA services. | Column names may align while nullable/default/concurrency semantics remain unverified; Oracle boolean conversions differ. | Provider-specific reconciliation and tests; do not remove apparently unused SQL objects. | UNVERIFIED |
| Views/functions/procedures | No required application stored-procedure contract was established; raw SQL is limited to provider sequence/locking operations and explicit attachment row lock. | `OracleTicketServices.cs`; `AttachmentService.cs`; repositories. | Absence from reviewed SQL does not prove absence from a live schema; unused objects must not be removed. | Maintain an inventory and owner review; no removal. | UNVERIFIED |

## Confirmed source discrepancies

1. `WaitingReasonRow.RequireExpectedDate` is mapped and returned by application code, but the reviewed PostgreSQL waiting-reason table definitions omit `N_REQUIRE_EXPECTED_DATE`. This would prevent faithful materialization or leave the model incompatible when that path is used.
2. `PA_TICKETS_WAITING_REASON` and its sequence are required by the current Oracle EF model but are not present in the reviewed Oracle baseline or migration 004. This is an object-level gap, not proof of live absence.
3. Reporting execution objects are code-required but belong to migration 026 candidates and are absent from the Oracle baseline. Their lifecycle, FK, sequence ownership, and provider policy remain separate owner decisions.

## Provider differences and risks

`DependencyInjection.cs:29-56` selects Npgsql or Oracle and configures both DbContexts. `ExecutionDbContext` uses PostgreSQL `jsonb` for report filters and provider-specific sequence defaults; Oracle maps boolean properties to numeric conversions. `OracleTicketServices.cs` issues `DUAL` sequence SQL while PostgreSQL uses `nextval`. These are separate implementations, not interchangeable syntax.

The repository contains multiple PostgreSQL schema scripts, a supplied Oracle baseline, ordered Oracle-style migrations, PostgreSQL migrations 028-038, and migration-026 reporting candidates. Their presence does not identify the applied order or live state. Idempotent-looking `IF NOT EXISTS` clauses do not prove definition equivalence; the T61 proposals therefore remain commented and require metadata checks before implementation.

## Proposed artifacts and limitations

- `Docs/reconciliation/proposed/postgresql/T61_waiting_reason_reconciliation.sql` documents the missing PostgreSQL column and required metadata/default checks.
- `Docs/reconciliation/proposed/oracle/T61_waiting_reason_reconciliation.sql` documents the missing Oracle table/sequence design without executable DDL or reference data.

Neither artifact claims idempotency, performs destructive changes, supplies data, or authorizes execution. No SQL syntax was submitted to a provider; static review cannot validate provider parser, transaction, or rollback behavior.

## Owner decisions and dependencies

Owners must decide the canonical provider baseline, migration ordering, waiting-reason reference data/defaults, sequence ownership and historical values, reporting-object ownership/FKs, RBAC migration 030-038 compatibility, audit actor/retention rules, and PostgreSQL versus Oracle release scope. Provider validation additionally requires an independently verified isolated target, approved fixtures, backup/baseline, storage/evidence custody, and explicit no-protected-target checks.

Migration 026 remains separately gated. Neither its original nor v3 candidate was executed, approved, or treated as applied.

## Commands and preservation

Read-only commands included `git branch --show-current`, `git status --short --ignored`, `Get-ChildItem`, `Get-Content`, `rg`, and `Get-FileHash`. No tests, builds, SQL execution, database/provider operation, migration, provisioning, seed, cleanup, Oracle access, or protected-target access occurred.

The branch remained `workstream-i-lifecycle-authority`. Status contained 201 entries before this gate and 203 after the result and two proposal artifacts were added. No pre-existing file was modified, staged, reset, cleaned, stashed, switched, committed, or pushed.

**GATE T61 PARTIAL — SOME OBJECTS CANNOT BE RECONCILED FROM SOURCE**
