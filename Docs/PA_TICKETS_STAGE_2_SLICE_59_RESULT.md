# PA_TICKETS Stage 2 Slice 59 — Remaining Evidence Gates

## Scope and sources

This was a documentation and repository inspection slice. I reviewed:

- `Docs/PA_TICKETS_STAGE_2_SLICE_58_RESULT.md`
- `Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md`
- `Docs/PA_TICKETS_POSTGRESQL_READ_ONLY_VALIDATION_AUTHORIZATION.md`
- `Docs/PA_TICKETS_STAGE_2_SLICE_57_APPROVAL_REQUEST.md`
- `database/migrations/014_incident_foundation.sql`

No database connection, SQL execution, migration, provider test, ledger write,
grant, cleanup, or configuration change occurred.

## Gate matrix

| Gate | Evidence present | Missing or contradictory evidence | Result |
|---|---|---|---|
| D01 / BASE | The decision pack records Strategy A, the historically verified artifact, Git provenance, and raw-byte SHA-256 `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19`. Slice 58 records the user's conditional `APPROVE` input. | No attributable owner identity/role, UTC decision date, scope/expiry, signature/reference, or final adoption record. Live catalog compatibility is not established. | `PENDING` |
| D09 | Slice 58 records the user's selected policy to retain history as `UNKNOWN` and fail closed. The prior preflight reported no ledger, but that is not proof of empty history. | No attributable D09 approval record, fixed validity, evidence custody, or fresh preflight under the current authorization. | `PENDING` |
| Migration 014 | Exact artifact and provenance are recorded: commit `0b595133286bae55b0b8fc079a631bd194cf43da`, Git blob `2943a7bf977772ce3619b3ae5eda473cf030547f`, raw Git-blob SHA-256 `08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3`. The user supplied conditional authorization in principle. | No operation-specific attributable authorization, operator, exact privileges/schema scope, execution window, expiry/revocation, recovery boundary, or evidence custody. | `NO-GO` |
| TGT | User-supplied identity output names `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`, with `::1`, PostgreSQL 18.6, recovery false, and SSL false. | The canonical read-only packet still records target conflict and requires attributable approval, approved hostname/peer/TLS policy, fixed UTC issue/expiry, and independent verification. User-supplied output is not an independent Codex verification. | `PENDING` |
| OP / D02 | Prior catalog evidence observed a non-superuser role and grants; the packet recommends a dedicated least-privilege operator. | No owner-approved operator identity, exact grants, schema scope, expiry, or independent current grant evidence. Observed privileges are not approval. | `PENDING` |
| PRE | Existing request defines read-only identity/catalog preflight and stop conditions. | The authorization packet is explicitly a request, not approval; no attributable scope, validity, revocation, or retention record exists. | `PENDING` |
| OP-TEST / migration execution | Slice 57 defines prohibited effects, recovery handling, and evidence requirements. | The conditional input does not authorize execution now; no explicit operation authorization binds the exact migration, target, operator, and recovery/evidence scope. | `NO-GO` |

The existing repository evidence therefore cannot separately authorize even a
fresh read-only preflight: the read-only packet says it is not approval and has
`PENDING` owner, target, scope, baseline, least-privilege, and retention fields
(`Docs/PA_TICKETS_POSTGRESQL_READ_ONLY_VALIDATION_AUTHORIZATION.md`, lines
7–28, 30–33, 47–57).

## Migration 014 transaction and recovery findings

The artifact creates a sequence, an Incident table, and three indexes (lines
2–37). It contains no `BEGIN`, `COMMIT`, savepoint, or recovery logic. A safe
execution therefore depends on an authorized runner explicitly opening one
transaction and keeping every statement in it. PostgreSQL can roll back these
ordinary, non-concurrent DDL statements when they are executed in one
transaction, but this source file alone does not establish that wrapper or the
provider's commit outcome.

The following source-level risks require the fresh preflight/recovery decision:

1. Every object uses `IF NOT EXISTS`. Existing objects with incompatible
   definitions could be silently accepted with notices rather than rejected;
   catalog definitions must be compared before execution and after commit.
2. The sequence default is present, but the script does not declare sequence
   ownership (`OWNED BY`) or reconcile an existing sequence's state. D03 and
   the provider-specific allocation plan remain required.
3. Actor and timestamp columns are not given foreign keys or semantic checks in
   this additive script. Compatibility with EF mappings, audit semantics, and
   security policy must be verified separately; the script must not be treated
   as a complete authorization contract.
4. Object names are unqualified. The authorized runner must establish the
   intended schema/search path and reject an unexpected target; this cannot be
   inferred from the file.
5. A statement, connection, or commit failure can leave the outcome unknown.
   The existing request correctly prohibits blind retry and requires
   independent catalog reconciliation. A rollback attempt is not proof of
   non-commit when the provider outcome is uncertain.

These are compatibility and execution controls, not permission to modify the
artifact. No ad hoc replacement table or ledger row is justified by this
review.

## Smallest next action

The accountable owner must complete one canonical approval record with:

- attributable identity/role, approval reference/signature, UTC issue date and
  fixed expiry;
- D01 adoption and D09 unknown-history decisions;
- the exact migration-014 artifact/hash, target, operator and least-privilege
  grants;
- a bounded execution window, revocation method, recovery/uncertain-commit
  procedure, and evidence-custody location/retention;
- explicit authorization for the read-only preflight before any write.

After that record is independently checked for consistency, the next technical
step is a fresh read-only target/catalog preflight. Migration execution and
Incident provider tests remain blocked until that preflight and the separate
operation-specific authorization pass.

## Validation and status

`git diff --check` was run. Its output contains pre-existing trailing-whitespace
and LF/CRLF conversion notices in the already dirty decision/documentation
files; this new report introduces no trailing whitespace. Stage 2 remains
`PARTIAL`, and provider execution remains `NO-GO`.
