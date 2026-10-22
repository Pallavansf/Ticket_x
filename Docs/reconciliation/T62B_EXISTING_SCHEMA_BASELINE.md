# T62B Existing PostgreSQL Schema Baseline

## Evidence boundary

The approved target was guarded before every connection:
`localhost:5432 / pa_tickets_e2e_verify / postgres`. The catalog observations
below are read-only and establish current object names and metadata only. They
do not establish migration provenance, equivalence to a script, or historical
application state.

## Reviewed source families

- `tests/scripts/postgresql-foundational-ddl.sql`
- `tests/scripts/postgresql-schema.sql`
- `tests/scripts/postgresql-slice1-schema.sql`
- `database/migrations/postgresql/028_*.sql` through `036_*.sql`
- `database/migrations/026_phase8_6_reporting.review*.sql`
- `src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs`
- `src/PA.Tickets.Infrastructure/Execution/ExecutionDbContext.cs`
- infrastructure repositories, authorization readers, reporting services, and
  raw provider SQL

The foundational scripts define the core ticket, work-item, waiting-reason,
assignment, event, audit, workflow, approval, SLA, idempotency, background-job,
department, team, user, role, permission, membership, and client objects.
PostgreSQL migration files 028-036 add profile, RBAC scope/delegation,
proposal, permission metadata, and migration-ledger objects. Migration 026 is
kept separate and remains unapproved.

## Catalog inventory

The guarded read-only catalog query found 36 public tables and 35 public
sequences, all owned by `postgres`. Present core tables include:

`PA_TICKETS`, `PA_TICKETS_WORK_ITEM`, `PA_TICKETS_WAITING_REASON`,
`PA_TICKETS_ASSIGNMENT`, `PA_TICKETS_EVENT`, `PA_TICKETS_AUDIT`,
`PA_TICKETS_AUDIT_CHAIN_LOCK`, `PA_TICKETS_AUDIT_MAINTENANCE`,
`PA_TICKETS_AUDIT_MAINTENANCE_TOKEN`, `PA_TICKETS_IDEMPOTENCY`,
`PA_TICKETS_BACKGROUND_JOB`, workflow/approval tables, SLA tables,
`PA_TICKETS_DEPARTMENT`, `PA_TICKETS_TEAM`, `PA_TICKETS_USER`,
`PA_TICKETS_USER_ROLE`, `PA_TICKETS_ROLE`, `PA_TICKETS_PERMISSION`,
`PA_TICKETS_ROLE_PERMISSION`, and `PA_TICKETS_USER_CLIENT`.

Present sequences include the ticket/work-item/user/RBAC/workflow/approval/SLA
families and `PA_TICKETS_WAITING_REASON_SEQ`. The catalog has no
`PA_TICKETS_SCHEMA_MIGRATION` ledger and no reporting tables named
`PA_TICKETS_REPORT_EXPORT`, `PA_TICKETS_REPORT_SCHEDULE`, or
`PA_TICKETS_REPORT_SCHEDULE_EXECUTION`.

## Object-level reconciliation

| Area | Reviewed SQL | Current catalog | Code dependency | Assessment |
|---|---|---|---|---|
| Core ticket/work item | Foundational DDL and test schema | Present | `TicketsDbContext`, `EfTicketRepository`, lifecycle services | Names align; exact columns/defaults/constraints still require definition comparison |
| Waiting reason | Foundational DDL includes table and `N_REQUIRE_EXPECTED_DATE` | Present, 10 columns | `PersistentWaitingReasonCatalog`, `TicketLifecycleService` | Source and catalog names align; provenance and exact defaults are unproven |
| Workflow/approval/SLA | Foundational DDL | Present | `TicketsDbContext`, execution services | Broad alignment; provider materialization and versioned constraints unverified |
| User/RBAC foundation | Foundational DDL and slice-1 script | Present | authorization facts readers and administration services | Core names align; later scope/delegation metadata is absent |
| RBAC scope/delegation/proposals | Migrations 031/032/035/036 define candidates | Not observed in catalog | `AdministrationUserRoleScopes`, `AdminHeadDelegations`, `RbacProposals` | Required upgrade is unresolved; do not replay blindly |
| Migration ledger | Migration 033 defines `PA_TICKETS_SCHEMA_MIGRATION` | Absent | migration/recovery contracts and preflight | No applied-history evidence exists |
| Reporting | Migration-026 candidates only | Absent | `ExecutionDbContext`, reporting services | Migration 026 remains separately gated and cannot be inferred as applied |
| Sequences/defaults | Foundational scripts and provider capabilities | Sequences present | EF sequence defaults and identity generators | Current values/ownership/default expressions were not established by this inventory |

## Compatibility and conflict risks

The 36-table catalog is consistent in broad naming with the foundational
PostgreSQL scripts, but `CREATE IF NOT EXISTS` cannot prove column, constraint,
index, default, sequence ownership, or case-sensitive definition equivalence.
The catalog’s 10-column waiting-reason table and 25/23-column ticket/work-item
shapes need column-level comparison before adoption.

The absent ledger means no checked-in migration can be treated as applied by
history. The absence of reporting and later RBAC objects means current code
paths depending on those objects cannot be declared provider-ready. Migration
ordering is also ambiguous because the foundational scripts contain data and
overlap the later migration family. Re-running any script could produce
conflicts or silently preserve incompatible definitions.

## Recommendation

**Adoption is not established.** The existing schema may be a compatible
foundational baseline, but the evidence is insufficient to adopt it or issue
an upgrade. The next safe step is a separately approved, read-only, column/
constraint/index/default/sequence-definition comparison against a named source
baseline, followed by an owner decision between formal adoption and a
non-destructive upgrade plan. No destructive reconciliation is justified.

## Least privilege

The current `postgres` superuser owns the observed objects and can perform the
requested DDL, but this exceeds minimum privilege. Any future build should
prefer a dedicated non-superuser database owner/operator with only CONNECT,
schema USAGE/CREATE, and explicitly required object/sequence privileges. No
role or privilege change is authorized by T62B.
