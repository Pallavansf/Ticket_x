# PA_TICKETS Stage 2 Slice 52 — Incident Schema Authorization Readiness

## Result and safety boundary

This slice prepared an owner-reviewable authorization package for the Incident
schema. It did not connect to PostgreSQL, inspect live catalogs, execute SQL,
apply migration 014, run migration 026, create schema objects, alter grants or
privileges, change sequences or data, or modify migration history. Stage 2
remains `PARTIAL`; Incident rollback and co-persistence remain unverified.

## Immutable artifact identity

The reviewed artifact is `database/migrations/014_incident_foundation.sql`.
Git records it in the initial source-baseline commit
`0b595133286bae55b0b8fc079a631bd194cf43da` under blob SHA-1
`2943a7bf977772ce3619b3ae5eda473cf030547f`. The exact Git blob is 1,629
bytes and its raw-byte SHA-256 is:

`08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3`

The checked-out working copy has the same semantic content but a different raw
working-file SHA-256 (`32B9ECF15658E3667FB5F8645F4FCC6703EF43C2E14221E356BBB8DAF47272F9`)
because its byte representation is subject to the repository's working-tree
line-ending conversion. `git diff --ignore-space-at-eol` reports no semantic
source difference. The immutable approval record should therefore identify the
Git commit/blob (or an exported immutable byte artifact), not an unqualified
working-copy hash.

## Objects and dependency contract

Migration 014 creates, in order (`database/migrations/014_incident_foundation.sql:1-37`):

1. `PA_TICKETS_INCIDENT_SEQ`.
2. `PA_TICKETS_INCIDENT` with primary key `N_INCIDENT_ID bigint default
   nextval(...)`, unique/non-null `N_TICKET_ID` referencing
   `PA_TICKETS(N_TICKET_ID)`, required impact/urgency/major flags, lifecycle
   timestamps and actor IDs, summary/outage fields, required created/updated
   timestamps, and nullable idempotency key.
3. `IX_PA_TICKETS_INCIDENT_IMPACT_URGENCY` and
   `IX_PA_TICKETS_INCIDENT_COMMANDER` (`:35-36`).
4. Partial unique `UX_PA_TICKETS_INCIDENT_IDEMPOTENCY` for non-null keys
   (`:37`).

The ticket foreign key makes the approved foundational `PA_TICKETS` table and
its compatible key contract a prerequisite. The artifact has no explicit
impact/urgency checks, commander/user foreign keys, audit-actor columns, or
row-version column. EF maps the same application-facing columns in
`src/PA.Tickets.Infrastructure/Persistence/IncidentRows.cs:6-36` and registers
the set in `TicketsDbContext.cs:9`; those mappings do not justify adding
unmapped columns or constraints.

The broader `tests/scripts/postgresql-phase5-schema.sql:4-17,124-156` contains
the same Incident sequence/table/index definition, but it is bundled with
other Phase 5 objects and is not an adopted or authorized Incident-only
artifact. It must not be used as a substitute for migration 014.

## Decisions required before execution

| Gate | Owner decision/evidence required | Current state |
|---|---|---|
| D01 / BASE | Attributable adoption or rejection of one immutable foundational DDL artifact, with hash, provenance, expected definition comparison, and treatment of unknown history. | `PENDING`; the decision pack contains a recommendation only. |
| Migration 014 | Explicit operation-specific approval naming the exact Git artifact/hash, target, operator, execution window, allowed effects, and expiry. | Missing; Git presence is not authorization. |
| TGT / OP | Current, attributable target identity and least-privilege non-superuser operator evidence, including exact schema privileges and expiry. | Must be independently current and scoped to this schema operation. |
| PRE | Formal read-only preflight authorization covering target identity, baseline, migration history, dependency checks, evidence retention, and expiry. | Read-only catalog authorization does not authorize DDL. |
| Execution/recovery | Ordered prerequisite check, unknown-history handling without fabricated ledger rows, transaction/rollback boundary, post-apply catalog and application validation, and recovery/escalation for uncertain outcome. | Not approved or executed. |

The owner must also decide whether migration 014 remains a separately versioned
additive change under Strategy A, or whether an immutable reviewed replacement
is preferred. D01 adoption alone cannot authorize migration 014. No repository
evidence proves that 014 or any equivalent Phase 5 script has previously been
applied to the approved database.

## Smallest safe next action

Record an attributable D01 decision and a separate migration-014 operation
approval using the exact Git provenance and hash above. Bind that approval to
the approved target/operator and a documented recovery boundary. After all
evidence is current and unexpired, perform only the authorized read-only
preflight/catalog comparison. Do not apply the migration until that comparison
and the execution authorization are complete; do not create an ad hoc table or
fabricate migration-ledger history.

## Validation

Artifact hash and Git identity were reproduced from repository bytes without
modifying the migration. `git diff --check` reports only pre-existing trailing
whitespace in the owner decision packet and existing LF/CRLF notices; the new
report introduces no intentional trailing whitespace. No tests were required
for this documentation-only slice, and no provider operation occurred.
