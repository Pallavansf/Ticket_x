# PA_TICKETS Stage 2 Slice 51 — `PA_TICKETS_INCIDENT` Schema Provenance

## Scope and safety

This was a source and repository-artifact investigation only. No PostgreSQL or
Oracle connection was opened, and no SQL, migration, DDL, DML, provisioning,
seed, cleanup, grant, privilege, sequence, or migration-026 operation was
executed. Existing working-tree changes were preserved. Stage 2 remains
`PARTIAL` and provider rollback/incident atomicity remain unverified.

## Defining artifacts

| Artifact | Evidence | Meaning |
|---|---|---|
| `database/migrations/014_incident_foundation.sql:1-37` | Creates `PA_TICKETS_INCIDENT_SEQ`, the table, two ordinary indexes, and a filtered unique idempotency index. | The repository's explicit additive PostgreSQL Incident schema artifact. |
| `src/PA.Tickets.Infrastructure/Persistence/IncidentRows.cs:6-36` | `[Table("PA_TICKETS_INCIDENT")]` and column attributes for the Incident row. | EF Core persistence contract consumed by `TicketsDbContext.Incidents`. |
| `src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs:9` | `DbSet<IncidentRow> Incidents`. | Registers the table in the infrastructure context; it does not create the table. |
| `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs:36-72,298-321` | Idempotency lookup, insert/staging, reads, and mutation paths use `db.Incidents`. | Runtime Incident operations require the table and its key relationship. |
| `tests/scripts/postgresql-phase5-schema.sql:4-17,124-156` | Includes the same Incident sequence/table/index shape inside a broader Phase 5 provisioning artifact. | A broader provider-specific schema script, not an adopted baseline and not authorized for execution here. |
| `tests/PA.Tickets.Domain.Tests/PostgreSqlIncidentTests.cs:15-169` and `PostgreSqlIncidentAtomicityTests.cs:44-178` | Provider tests read/write Incident rows and use test-owned cleanup; the atomicity fixture checks for the table before writing. | Test prerequisites and safety evidence, not schema provenance. |

The current foundational artifact `tests/scripts/postgresql-foundational-ddl.sql`
contains no `PA_TICKETS_INCIDENT` definition, sequence, or index. The Oracle
artifacts `database/schema/001_PA_TICKETS_SCHEMA.sql` and
`Docs/PA_TICKETS_SCHEMA.sql` likewise contain no occurrence of the PostgreSQL
table name. This is consistent with the existing DDL compatibility assessment,
which records Incident/Problem tables as separate migrations rather than
foundational objects (`Docs/PA_TICKETS_STAGE_2_DDL_COMPATIBILITY_ASSESSMENT.md:23,64`).

## Required schema contract

`014_incident_foundation.sql:2-32` defines:

- sequence `PA_TICKETS_INCIDENT_SEQ`;
- primary key `N_INCIDENT_ID bigint default nextval(...)`;
- required one-to-one ticket link `N_TICKET_ID bigint not null unique references PA_TICKETS(N_TICKET_ID)`;
- required impact and urgency values (`varchar(30)`, defaults `MEDIUM`) and required major-incident flag (default `false`);
- nullable commander, acknowledgement, restoration, resolution, closure, major-declaration, summary, and outage columns;
- required `D_CREATED_AT` and `D_UPDATED_AT` timestamps; and nullable `S_IDEMPOTENCY_KEY varchar(200)`;
- `IX_PA_TICKETS_INCIDENT_IMPACT_URGENCY` and `IX_PA_TICKETS_INCIDENT_COMMANDER` (`:35-36`); and
- filtered unique `UX_PA_TICKETS_INCIDENT_IDEMPOTENCY` for non-null idempotency keys (`:37`).

The EF row maps exactly the columns it uses (`IncidentRows.cs:9-36`); it does
not map additional audit-actor, created-by, updated-by, or row-version columns.
Those columns are therefore not a proven requirement of this current Incident
EF contract. The migration does not add foreign keys from commander or actor
columns to a user table, and no check constraints for impact/urgency are
present. Those are observable design characteristics requiring review, not
grounds for inventing a replacement table definition.

The Phase 5 script repeats the Incident sequence and table shape
(`postgresql-phase5-schema.sql:12-13,124-156`) and adds the same three indexes.
It is embedded in a larger script that also creates other Phase 5 objects, so
it is not an independently approved Incident-only artifact. No source evidence
shows that either artifact was applied to the target or that their migration
history is recorded.

## Dependency and provenance chain

1. The Incident service requires the existing `PA_TICKETS` row first, then
   stages the Incident row, event, and audit (`IncidentService.cs:43-72`).
2. Migration 014 references `PA_TICKETS(N_TICKET_ID)` and therefore depends on
   the foundational ticket table and its sequence being present and compatible
   (`014_incident_foundation.sql:4-6`).
3. The Incident sequence/table must exist before the provider Incident tests can
   execute. The test fixture's read-only `to_regclass` guard stopped both Slice
   50 Incident cases before writes when the table was absent
   (`PostgreSqlIncidentAtomicityTests.cs:171-178`; Slice 50 report, lines 18,
   34-36). This is prior provider evidence, not a new connection or current
   state claim in Slice 51.
4. The repository contains no authoritative applied-migration ledger entry or
   migration runner evidence for 014. Git history shows the file in the initial
   source baseline commit `0b595133286bae55b0b8fc079a631bd194cf43da`; that is
   artifact provenance only and does not prove execution or approval.

Migration 015 is a separate Problem artifact and is not a prerequisite for the
Incident table itself. The roadmap keeps Incident/Problem persistence as a
Stage 2 provider dependency and requires provider-backed persistence,
failure-injection, audit-chain, and lifecycle API evidence
(`Docs/PA_TICKETS_MASTER_ROADMAP.md:62-98`).

## Baseline and approval reconciliation

The D01 proposal recommends Strategy A: adopt the historically verified
foundational DDL and handle later additions as separately reviewed, versioned
changes (`Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md:55-84`). The
Slice 35 assessment explicitly classifies Incident/Problem as migrations
014/015, not as foundational content (`Docs/PA_TICKETS_STAGE_2_DDL_COMPATIBILITY_ASSESSMENT.md:68-85`).
No D01 attestation or operation-specific schema approval in the decision pack
authorizes applying 014. Baseline adoption alone would not authorize a schema
change, and the Phase 5 provisioning script is expressly outside this slice's
authorization.

The precise unresolved requirements are:

1. An attributable D01 baseline adoption/rejection with immutable artifact,
   hash, provenance, and expected definition comparison.
2. An explicit, separately authorized decision to apply a reviewed version of
   migration 014 (or another exact, traceable Incident artifact), including
   ordering after the approved ticket foundation, migration-ledger treatment,
   rollback/recovery and unknown-history handling.
3. Current TGT/OP/PRE evidence for the named target and least-privilege schema
   operator, plus operation-specific authorization for the schema change. The
   existing read-only preflight authorization does not authorize DDL.
4. After those gates, a read-only catalog comparison and then a separately
   authorized execution/validation of Incident persistence and rollback. No
   ad hoc table, seed data, sequence reset, or fabricated ledger row is safe.

## Smallest safe next action

Do not edit the foundational DDL or create a replacement table. Have the owner
resolve D01 and approve a versioned, reviewed Incident schema change based on
migration 014 (including its exact hash, dependency order, constraints/defaults,
and recovery boundary). Once that authorization and independent target/operator
evidence are attributable and current, perform only the authorized read-only
catalog comparison before any schema execution. Until then the missing table is
an explicit provider/schema blocker for Slice 50 Incident atomicity tests.

## Validation

`git diff --check` was run after creating this report. Any trailing-whitespace
and LF/CRLF notices reported in the existing decision packet and other
pre-existing files remain unchanged; the new report contains no intentional
trailing whitespace. No source, migration, DDL, configuration, or formal
decision status was changed.
