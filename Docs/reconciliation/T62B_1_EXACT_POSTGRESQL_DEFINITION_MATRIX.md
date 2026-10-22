# T62B.1 Exact PostgreSQL Definition Matrix

## Target and evidence

Every catalog query was guarded to `localhost:5432`, database
`pa_tickets_e2e_verify`, role `postgres`; prohibited databases and roles were
rejected before connection. The server reported PostgreSQL 18.6 on `::1`.
Queries read catalog metadata only and did not read application rows.

## Source artifact hashes

| Artifact | SHA-256 |
|---|---|
| `tests/scripts/postgresql-foundational-ddl.sql` | `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7` |
| `tests/scripts/postgresql-schema.sql` | `2CCC5708C27C9DACDF26EE2B2E417832CEED9AC7DFC97D8354CF204C04AE41BF` |
| `tests/scripts/postgresql-slice1-schema.sql` | `26A6F76C0B82F85A74415733FBC11EF36A8DC93FAF60B39B7EB8CF699669129B` |
| `database/migrations/026_phase8_6_reporting.sql` | `791C460D63274AC4187ECB3B1861D16E8CE73D955426F0560C39DC5470047AD4` |

The resolved PostgreSQL migration files 028 through 038 were also enumerated
and hashed; their exact paths and hashes are recorded in the T62B.1 result.

## Object matrix

| Object | Expected source | Observed definition | Dependencies | Status |
|---|---|---|---|---|
| `PA_TICKETS` | foundational DDL/schema | 25 columns; bigint sequence default; timestamp-with-time-zone fields; integer defaults for counters/row version; primary key and ticket-number unique index | `TicketRow`, lifecycle/repository code | MATCH at reviewed fields; full semantic adoption still requires baseline approval |
| `PA_TICKETS_WORK_ITEM` | foundational DDL/schema | 23 columns; bigint sequence default; waiting-minute default; ticket FK; primary/public/work-item number uniqueness | `WorkItemRow`, `EfTicketRepository` | MATCH at reviewed fields and constraints |
| `PA_TICKETS_WAITING_REASON` | foundational DDL/schema | 10 columns; boolean defaults (`active=true`, pause=true, requirement flags=false); `InProgress` status default; effective lookup index; primary key; sequence default | `PersistentWaitingReasonCatalog`, `TicketLifecycleService` | MATCH |
| Core workflow/approval/SLA tables | foundational DDL/schema | Tables, primary keys, foreign keys, checks, and indexes observed under expected names | execution and lifecycle services | PARTIAL: exact source-to-catalog definition equivalence not approved |
| User/role/permission foundation | foundational DDL and slice1 | Foundation tables and sequences present; role/permission base columns present | authorization fact readers | PARTIAL: migration 031+ columns and canonical indexes are not all present |
| `PA_TICKETS_USER_ROLE_SCOPE` | migrations 031/035 | Not observed | organization/scope authorization readers | MISSING |
| `PA_TICKETS_ADMIN_HEAD_DELEGATION` | migrations 030/031/035 | Not observed | delegation authorization | MISSING |
| `PA_TICKETS_RBAC_PROPOSAL` | migrations 032/036 | Not observed | proposal service/API | MISSING |
| `PA_TICKETS_SCHEMA_MIGRATION` | migration 033 | Not observed | applied-history and preflight contracts | MISSING; migration history cannot be inferred |
| Permission metadata/effective-grant columns | migrations 031/034 | Expected additive columns were not present in the observed base role/permission shape | `DatabaseAuthorizationFactsReader` | CONFLICT/UPGRADE REQUIRED |
| Reporting tables | migration 026 original/candidates | `PA_TICKETS_REPORT_EXPORT`, `PA_TICKETS_REPORT_SCHEDULE`, and `PA_TICKETS_REPORT_SCHEDULE_EXECUTION` absent | `ExecutionDbContext`, reporting services | MISSING; migration 026 remains separately gated |

## Constraints and indexes

Catalog metadata exposed primary keys for the observed tables, expected ticket
and work-item foreign keys, unique ticket/work-item/role/permission identities,
active-assignment and idempotency partial indexes, effective membership indexes,
and the waiting-reason effective lookup index. These names and predicates are
consistent with the foundational scripts where present. Later canonical RBAC
indexes and effective-grant checks are migration-dependent and cannot be
treated as applied.

## Sequences and defaults

Observed sequences use start `1`, increment `1`, cache `1`, and `NO CYCLE`.
The catalog reported no owned-by relation for the inspected sequences, and
`last_value` was unset. Defaults on core table IDs call their corresponding
quoted sequence. Unset sequence state does not prove that IDs are unused or
safe to allocate; no sequence was advanced, reset, or otherwise modified.

## Views, functions, and triggers

No user views/materialized views, user-defined public functions, or non-internal
triggers were observed in the catalog query. This does not authorize removal or
prove that application behavior is independent of such objects.

## Migration findings

- Migration 031 requires uppercase role/permission identities, adds effective
  grant metadata, and performs data backfills; it cannot be replayed without
  duplicate/canonical-code and historical-time decisions.
- Migration 033’s ledger is absent, so no migration can be considered applied
  by catalog presence.
- Migration 034 requires unknown grants to remain non-authorizing and depends
  on 033/approved baseline metadata.
- Migration 035 requires parent membership/role/user relations and defers
  client-FK semantics; its scope and delegation tables are absent.
- Migration 036 depends on the ledger and permission metadata; its proposal
  table is absent.
- Migration 037 is explicitly approval-gated and provisions no grants; it was
  not inferred or executed.
- Migration 038 is intentionally blocked by unresolved audit-actor semantics.

## Upgrade risk and required evidence

Any upgrade must preserve existing rows, keys, audit history, sequence
allocation, and effective dates. Before an approved additive operation, require
column/default/constraint/index equivalence reports, sequence ownership and
allocation decisions, a baseline/adoption record, rollback/recovery evidence,
and explicit owner approval for data backfill and built-in role provisioning.
No current evidence supports a safe execution order beyond “baseline
definition/adoption first, then separately approved additive migrations.”
