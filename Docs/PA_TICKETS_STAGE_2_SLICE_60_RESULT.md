# PA_TICKETS Stage 2 Slice 60 — Authorization Readiness and Runner Contract

## Scope and canonical record

This slice reviewed the existing owner decision pack, PostgreSQL read-only
authorization packet, Slice 57 request, Slice 58–59 results, migration 014,
and the checked-in PostgreSQL test guard. No database connection, SQL,
migration, provider test, ledger write, grant, cleanup, or configuration change
occurred.

`Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md` remains the canonical
decision record for D01, D09, TGT, OP/D02, and PRE. Operation-specific fields
for migration 014 and `PostgreSqlIncidentAtomicityTests` remain in
`Docs/PA_TICKETS_STAGE_2_SLICE_57_APPROVAL_REQUEST.md`; that request is linked
evidence, not a second approval authority. Slice 58 inputs remain explicitly
user-confirmed and conditional; formal statuses are unchanged.

## Required completion fields

| Area | Required fields | Current state |
|---|---|---|
| D01 / BASE | Decision; exact artifact/provenance/hash; compatibility; owner identity/role; UTC date; scope/expiry; signature/reference; rationale/evidence | User-confirmed recommended `APPROVE` only; formal `PENDING` |
| D09 | Explicit `UNKNOWN` history and recovery policy; identity/role; UTC validity; signature/reference; evidence custody | User-confirmed recommended `APPROVE` only; formal `PENDING` |
| TGT | Database/role; host/peer policy; TLS/SNI policy; authentication; scope; UTC issue/expiry; revocation; attributable approval | User-supplied output exists, but packet remains `PENDING / TARGET CONFLICT`; not independently verified here |
| OP / D02 | Named non-superuser; exact grants/schema scope; effects; expiry/revocation; independent grant evidence | Observations are not approval; `PENDING` |
| PRE | Explicit read-only identity/catalog scope; target binding; owner/reference; expiry/revocation; retention | Existing packet is a request, not authorization; `PENDING` |
| Migration 014 | Exact artifact/hash; one-time scope; operator; schema scope; UTC window; expiry/revocation; transaction/recovery; evidence custody | Conditional authorization in principle only; `NO-GO` |
| OP-TEST | Explicit authorization for `PostgreSqlIncidentAtomicityTests`, unique writes, cleanup/recovery, and evidence retention | Not supplied; provider Incident tests blocked |

Migration identity is `database/migrations/014_incident_foundation.sql`, commit
`0b595133286bae55b0b8fc079a631bd194cf43da`, Git blob
`2943a7bf977772ce3619b3ae5eda473cf030547f`, raw Git-blob SHA-256
`08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3`.

## Source-verified runner feasibility

Migration 014 contains one sequence, one table, and three non-concurrent
indexes (`database/migrations/014_incident_foundation.sql`, lines 2–37). It has
no transaction wrapper. Infrastructure uses EF Core 8.0.11 and
`Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11 (`src/PA.Tickets.Infrastructure/PA.Tickets.Infrastructure.csproj`, lines
17–20). `AuditTransaction.BeginIfNeededAsync` returns `null` when a caller
transaction exists and otherwise starts one (`src/PA.Tickets.Infrastructure/Persistence/AuditTransaction.cs`, lines 8–17).
The PostgreSQL test guard requires explicit connection, write opt-in, and an
exact non-secret target descriptor while rejecting prohibited targets
(`tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironment.cs`, lines 9–46).

The feasible execution contract is:

1. Preserve exact migration bytes and verify the authorized target.
2. Open one explicit transaction before the first statement and verify schema/search path.
3. Compare expected object definitions before execution; reject incompatible existing objects rather than relying on `IF NOT EXISTS`.
4. Execute only migration 014. Do not run other migrations, seeds, ledger writes, grants, sequence resets, or cleanup.
5. Commit once; report success only after commit succeeds, with no automatic retry.
6. For definite pre-commit failure, preserve the primary exception, attempt one rollback, and verify the catalog read-only. Failed rollback or verification is `UNKNOWN`.
7. For connection, commit, or disposal failure with uncertain outcome, stop as `UNKNOWN`, do not retry, and require independent reconciliation.
8. After confirmed commit, retain artifact hash, target/operator evidence, transaction outcome, and complete object definitions.

This is a design requirement, not an implemented runner or execution approval.

## Compatibility questions before implementation

- `IF NOT EXISTS` does not compare definitions; pre-existing sequences, tables,
  and indexes must be rejected when incompatible.
- The script does not declare sequence ownership (`OWNED BY`) or reconcile
  sequence state; D03 remains required.
- Object names are unqualified, so schema/search-path identity must be fixed
  and verified before execution.
- Actor/user columns have no foreign keys or semantic checks in this artifact;
  EF, audit, and security compatibility requires separate evidence.
- The file does not establish migration history; no synthetic ledger row is
  permitted.
- PostgreSQL can roll back these ordinary statements when wrapped in one
  transaction, but commit/connection uncertainty still requires `UNKNOWN`
  reconciliation.

## Preconditions and decision

Before a read-only preflight, the canonical record must contain attributable,
current, unexpired TGT and PRE authorization, a selected baseline/provenance,
and an approved operator scope. Before migration execution, the exact artifact,
operator, one-time window, recovery contract, and evidence custody must be
approved. Before Incident provider tests, migration 014 must have a confirmed
outcome and post-apply catalog comparison, plus explicit OP-TEST authorization
for generated records and cleanup.

Current result: **NO-GO** for read-only preflight, migration execution, and
provider Incident tests. Stage 2 remains **PARTIAL**.

## Validation

`git diff --check` was run. It reports pre-existing trailing-whitespace and
LF/CRLF conversion notices in already modified files; this new report contains
no trailing whitespace. No formal decision status was changed.
