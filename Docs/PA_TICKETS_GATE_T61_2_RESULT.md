# PA_TICKETS Gate T61.2 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** Oracle waiting-reason persistence contract investigation  
**Database/provider activity:** none

## Executive result

Source inspection establishes the application-facing shape of
`PA_TICKETS_WAITING_REASON`, its primary-key requirement, and its read
semantics. It does not establish an Oracle deployment artifact, identifier
allocation strategy, Oracle boolean conversion, defaults, effective-date
overlap policy, reference data, or deployment owner. The result is therefore
**PARTIAL** and must not be treated as Oracle runtime validation.

## Source trace and table contract

`src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs:20` maps
`WaitingReasonRow` to `PA_TICKETS_WAITING_REASON`, calls `HasKey(x => x.Id)`,
and maps nine properties without `HasDefaultValueSql`, value conversion,
indexes, alternate keys, foreign keys, or check constraints.

`src/PA.Tickets.Infrastructure/Tickets/PersistentWaitingReasonCatalog.cs:26-38`
defines the CLR contract:

| Column | CLR type/nullability | Source-derived requirement | Oracle representation status |
|---|---|---|---|
| `N_WAITING_REASON_ID` | `long`, key, non-null | Primary key required by EF key mapping | `NUMBER(18)` follows repository convention; provider confirmation required |
| `S_REASON_CODE` | non-null `string` | Required for code lookup | `VARCHAR2(100 CHAR)` is consistent with the PostgreSQL schema and naming convention; Oracle mapping confirmation required |
| `N_ACTIVE` | non-null `bool` | Required in active/effective predicate | Numeric Oracle representation is convention only; conversion and allowed values require provider evidence |
| `D_EFFECTIVE_FROM` | non-null `DateTimeOffset` | Required lower time bound | `TIMESTAMP WITH TIME ZONE` matches repository timestamp convention |
| `D_EFFECTIVE_TO` | nullable `DateTimeOffset?` | Nullable upper time bound | `TIMESTAMP WITH TIME ZONE` nullable |
| `N_PAUSE_SLA` | non-null `bool` | Returned in the definition | Numeric representation/default unresolved |
| `N_REQUIRE_COMMENT` | non-null `bool` | Enforced by the wait service | Numeric representation/default unresolved |
| `N_REQUIRE_EVIDENCE` | non-null `bool` | Enforced by the wait service | Numeric representation/default unresolved |
| `N_REQUIRE_EXPECTED_DATE` | non-null `bool` | Enforced by the wait service | Numeric representation/default unresolved |
| `S_ALLOWED_FROM_STATUSES` | non-null `string` | Comma-separated status names parsed by the catalog | `VARCHAR2(200 CHAR)` is a proposal based on the PostgreSQL schema; Oracle length remains to be confirmed |

`PersistentWaitingReasonCatalog.cs:10-22` trims and uppercases the supplied
code, then requires exactly one active row effective at the supplied time via
`SingleOrDefaultAsync`. It parses comma-separated `AllowedFromStatuses` and
silently ignores unrecognized enum values. `TicketLifecycleService.cs:106`
uses the resolved flags to require comment, evidence, and expected date before
mutating the ticket. No production write path to `WaitingReasonRow` was found.

The reviewed Oracle baseline (`database/schema/001_PA_TICKETS_SCHEMA.sql`) and
`database/migrations/004_work_item_waiting.sql` do not define this table or a
related sequence. `Docs/PA_TICKETS_SCHEMA.sql` is a copy/document artifact and
its `S_REASON_CODE` occurrence is the existing `PA_TICKETS_SLA_PAUSE` table,
not a waiting-reason table. This proves only that the object is absent from the
reviewed source scripts; it does not establish live database state.

## Keys, defaults, constraints, and indexes

- **Primary key:** supported by `HasKey` and the `Id` property. The exact
  Oracle numeric precision is convention-based and needs provider review.
- **Identifier generation:** unresolved. `WaitingReasonRow.Id` has no EF
  sequence annotation, `HasDefaultValueSql`, identity configuration, or
  application insert path. Existing PostgreSQL fixtures assign IDs explicitly.
  Oracle sequence conventions in `database/migrations/002_hardening.sql` do
  not prove a sequence is required here. No sequence name, increment, cache,
  identity, or application allocation rule may be selected from this evidence.
- **Defaults:** unresolved. CLR initializers (`AllowedFromStatuses =
  "InProgress"`) apply to newly constructed objects, not necessarily database
  rows. EF does not configure database defaults. The prior Oracle proposal’s
  defaults were removed from its commented sketch.
- **Foreign keys/checks:** none are established by the mapping or consumers.
  Numeric flag checks and status-value checks remain owner/provider decisions.
- **Uniqueness/indexes:** `SingleOrDefaultAsync` requires an operational data
  invariant that at most one row matches code plus active/effective time, but
  the source does not define whether this is enforced by uniqueness, a
  non-overlap rule, data stewardship, or an index. No Oracle index or unique
  constraint is proposed.
- **Audit fields/timestamps:** the entity has only effective timestamps; no
  created/updated/audit columns are mapped or consumed.

## Reference data and deployment ownership

The application requires at least one effective row for any requested waiting
reason, otherwise resolution throws `InvalidOperationException`. No
source-controlled Oracle reference-data rows, seed script, deployment manifest,
or accountable deployment team for this table was found. PostgreSQL tests seed
rows directly in a provider-backed fixture, but that does not establish Oracle
reference data or ownership. Canonical codes, historical backfill, effective
range policy, and deployment owner are unresolved.

## Proposal change

`Docs/reconciliation/proposed/oracle/T61_waiting_reason_reconciliation.sql`
was revised only to remove unsupported `DEFAULT` clauses and the speculative
numeric-flag check from its commented table sketch. The primary key and mapped
columns remain as a non-executable source-contract sketch. Sequence,
identifier allocation, defaults, checks, uniqueness, overlap enforcement,
indexes, foreign keys, reference data, and deployment ownership remain
explicitly unresolved. No PostgreSQL proposal, application code, migration, or
candidate was changed.

## Remaining owner decisions and evidence

1. **Oracle mapping owner / provider team:** approve bool-to-Oracle mapping,
   `NUMBER` precision/length semantics, string lengths, timestamp materialization,
   and null handling; evidence: Oracle/Npgsql-compatible EF mapping review and
   provider-backed materialization tests.
2. **Database owner:** choose key allocation (explicit application key,
   sequence, identity, or another approved mechanism), including object name,
   increment/cache behavior and privileges; evidence: approved DDL and insert
   contract.
3. **Data/governance owner:** define code uniqueness and effective-date overlap
   policy, canonical codes, and historical/reference-data deployment; evidence:
   approved data contract and non-destructive deployment plan.
4. **Deployment owner:** identify the migration/artifact owner and ordering,
   preflight, rollback, and transaction policy; evidence: reviewed deployment
   runbook and provider-specific rehearsal.
5. **Migration-026 authority:** reporting migration ownership, exact hash,
   target identity, and separate execution approval remain blocked as recorded
   by T61/T61.1.

## Static validation performed

Read-only `git branch --show-current`, `git status --short --ignored`,
`Get-Content`, and `rg` inspections covered the T61/T61.1 results, Oracle
proposal, EF mapping, entity/contract/catalog/service, repositories and
consumers, Oracle baseline and migrations, schema conventions, provider
configuration, and relevant test fixtures. No tests, build, SQL parser,
provider, Oracle, PostgreSQL, migration, provisioning, seed, cleanup, or
protected-target operation was run.

## Preservation confirmation

The required branch remained `workstream-i-lifecycle-authority`. The complete
pre-existing modified, untracked, and ignored worktree was preserved. No reset,
clean, stash, branch switch, stage, commit, or push was performed. Final status
was rechecked after the documentation/proposal updates.

**GATE T61.2 PARTIAL — OWNER DECISIONS OR SOURCE EVIDENCE REQUIRED**
