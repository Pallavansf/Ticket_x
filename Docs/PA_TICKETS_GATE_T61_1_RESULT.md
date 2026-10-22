# PA_TICKETS Gate T61.1 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** reconciliation review and resolution planning  
**Database/provider activity:** none

## Executive result

The PostgreSQL proposal was corrected because both reviewed PostgreSQL schema scripts already contain `N_REQUIRE_EXPECTED_DATE`. The Oracle proposal was narrowed to the source-supported table mapping; its prior sequence and index sketches were removed as speculative because EF does not configure identifier generation or those constraints. The remaining Oracle table-ownership, numeric-flag, effective-range, reference-data, and deployment decisions require owner approval.

## Workstream A — PostgreSQL waiting-reason field

### Source trace

- `WaitingReasonRow.RequireExpectedDate` is a non-nullable CLR `bool` (`src/PA.Tickets.Infrastructure/Tickets/PersistentWaitingReasonCatalog.cs:26-37`).
- EF maps it to `N_REQUIRE_EXPECTED_DATE` (`src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs:20`). No conversion or provider-specific override is present.
- `PersistentWaitingReasonCatalog.ResolveAsync` reads active/effective rows and returns the flag in `WaitingReasonDefinition` (`PersistentWaitingReasonCatalog.cs:10-22`).
- The ticket wait path consumes the resolved definition; repository persistence writes the work-item waiting fields, while no production path writes `WaitingReasonRow` itself.
- Provider-backed persistence tests seed/read `RequireExpectedDate` true and false (`tests/PA.Tickets.Domain.Tests/PostgreSqlTicketPersistenceTests.cs:305-315`) and assert the waiting result (`lines 342-347`). Those tests were not run in this gate.

### Schema conclusion

Both reviewed PostgreSQL scripts already define the field:

- `tests/scripts/postgresql-schema.sql:61-72`: `"N_REQUIRE_EXPECTED_DATE" boolean not null default false`
- `tests/scripts/postgresql-foundational-ddl.sql:74-85`: same definition

No equivalent alternate field name was found or needed. The prior T61 claim that the field was absent was incorrect and is corrected in the revised PostgreSQL proposal. No default, backfill, constraint, or index change is justified from source evidence. `false` is source-supported as the schema-script default, but business policy for historical rows remains a provider/owner decision.

## Workstream B — Oracle waiting-reason table and sequence

### Source-supported table mapping

`TicketsDbContext.cs:20` maps `WaitingReasonRow` to `PA_TICKETS_WAITING_REASON` with key convention and these columns:

| CLR property | Column | CLR type/nullability | Source-supported Oracle shape |
|---|---|---|---|
| `Id` | `N_WAITING_REASON_ID` | `long`, non-null key | `NUMBER(18)` primary key by repository convention |
| `Code` | `S_REASON_CODE` | non-null `string` | `VARCHAR2(100 CHAR)`; length follows PostgreSQL schema and CLR usage |
| `Active` | `N_ACTIVE` | non-null `bool` | Numeric boolean representation is provider convention, but exact default requires owner approval |
| `EffectiveFrom` | `D_EFFECTIVE_FROM` | non-null `DateTimeOffset` | `TIMESTAMP WITH TIME ZONE` |
| `EffectiveTo` | `D_EFFECTIVE_TO` | nullable `DateTimeOffset?` | `TIMESTAMP WITH TIME ZONE` nullable |
| `PauseSla` | `N_PAUSE_SLA` | non-null `bool` | Numeric boolean representation; default not independently approved |
| `RequireComment` | `N_REQUIRE_COMMENT` | non-null `bool` | Numeric boolean representation |
| `RequireEvidence` | `N_REQUIRE_EVIDENCE` | non-null `bool` | Numeric boolean representation |
| `RequireExpectedDate` | `N_REQUIRE_EXPECTED_DATE` | non-null `bool` | Numeric boolean representation |
| `AllowedFromStatuses` | `S_ALLOWED_FROM_STATUSES` | non-null `string` | `VARCHAR2(200 CHAR)` with default only if approved |

`PersistentWaitingReasonCatalog.cs:14-22` requires code/time lookup semantics and parses comma-separated status names. It does not establish a database unique constraint, foreign key, index, overlap exclusion, or write-side invariant. The reviewed Oracle baseline and migration 004 do not contain this table or sequence; that means only “not found in reviewed SQL,” never live absence.

### Sequence assessment

The prior proposal’s `CREATE SEQUENCE PA_TICKETS_WAITING_REASON_SEQ` was removed. `WaitingReasonRow.Id` has no `HasDefaultValueSql` mapping, unlike many other entities, and no production repository writes waiting-reason rows. Existing PostgreSQL fixtures assign IDs explicitly. Oracle migrations commonly use `START WITH 1 INCREMENT BY 1 NOCACHE`, but that convention alone does not prove this table needs a sequence. Identifier allocation, if future writes are introduced, is an owner decision.

### Constraints and indexes

The EF key convention supports a primary key. No source supports a unique code constraint because effective-dated rows may coexist, and no source defines overlap prevention. The PostgreSQL index is an implementation convenience in test schemas, not proof of an Oracle requirement. The revised Oracle proposal therefore contains no sequence, unique constraint, index, foreign key, or reference data.

## Workstream C — Migration 026

Reporting tables remain a separate migration-026 dependency. `ExecutionDbContext.cs:10-13` and reporting services require export/schedule objects, while the checked-in Oracle baseline does not contain them. The exact artifact hash, target identity, owner/FK policy, sequence ownership, strict-equivalence preflight, transaction/rollback contract, provider-specific implementation, and execution approval remain required. No migration 026 file was edited, generated, approved, enabled, or executed.

## Proposal assessment and changes

- PostgreSQL proposal revised to a no-change reconciliation; it now records the field as already present and removes the unsupported `ALTER TABLE` sketch.
- Oracle proposal revised to retain only a commented table shape and explicitly mark identifier allocation, defaults, uniqueness, indexing, reference data, and deployment behavior as unresolved decisions.
- Earlier T61 result and all existing migrations/schema files remain unchanged.

## Static checks and limitations

Read-only commands used `git branch --show-current`, `git status --short --ignored`, `Get-Content`, `Get-ChildItem`, and `rg`. Static checks compared CLR properties, EF mappings, service reads/writes, provider scripts, migration conventions, and test fixtures. No SQL parser, provider, database, test, build, migration, provisioning, seed, cleanup, Oracle access, or protected-target operation was used.

Static source cannot establish live object presence, Oracle boolean conversion, sequence state, effective-range overlap policy, transaction/rollback behavior, or provider materialization. Those remain blocked until separately approved provider evidence.

## Preservation confirmation

The branch remained `workstream-i-lifecycle-authority`. Status contained 203 entries before this gate and 204 after the result document was added. Existing modified, staged, untracked, and ignored work was preserved; no reset, clean, stash, branch switch, stage, commit, or push occurred.

**GATE T61.1 PARTIAL — OWNER DECISIONS OR SOURCE EVIDENCE REQUIRED**
