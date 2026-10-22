# PostgreSQL RBAC migration 031/032 repair â€” review draft

**Status: non-executable decision record.** This draft records source evidence and a proposed
repair shape. It is not a migration, migration approval, or evidence of deployed schema state.
The PostgreSQL 030/031/032 files remain unexecuted. No database was accessed for this review.

## Decisions that can be resolved from repository sources

### Client entity and key

The source-controlled canonical schema defines `PA_TICKETS_CLIENT` with primary key
`N_CLIENT_ID`, unique `S_CLIENT_CODE`, `S_CLIENT_NAME`, description, status, contact and
classification fields, and created/updated timestamps and user IDs. It has foreign-key
relationships from `PA_TICKETS_USER_CLIENT.N_CLIENT_ID`, `PA_TICKETS.N_CLIENT_ID`, and
`PA_TICKETS_ANNOUNCEMENT_TARGET.N_CLIENT_ID`. `database/migrations/002_hardening.sql` defines
`PA_TICKETS_CLIENT_SEQ` for the Oracle schema; that does not establish a PostgreSQL sequence or
its allocation policy.

Therefore the repository's authoritative client entity/key is **`PA_TICKETS_CLIENT` /
`N_CLIENT_ID`**, not an invented substitute. This resolves the conceptual entity, but not the
PostgreSQL data source or migration: the previous authorized catalog preflight found no
`PA_TICKETS_CLIENT` relation, and this task did not reconnect. Existing user-client/ticket rows
may contain client IDs that need a preserved mapping. Creating an empty table and adding FKs could
orphan/block existing data; synthesizing client names/codes or remapping IDs is not acceptable.

**Required deployment decision:** identify the approved PostgreSQL client-catalog source and
ID-allocation policy; supply a preservation mapping for all existing client IDs before adding
client FKs. If there is no existing source data to import, explicitly authorize how the initial
catalog is established. Use restrictive/no-cascade deletion and soft retirement only if confirmed
as the intended lifecycle.

### Creator and effective-date fields

`database/schema/001_PA_TICKETS_SCHEMA.sql` defines:

| Entity | Creation fields | Update fields |
|---|---|---|
| `PA_TICKETS_ROLE` | `D_CREATED_AT NOT NULL`, `N_CREATED_BY NOT NULL` | `D_UPDATED_AT NULL`, `N_UPDATED_BY NULL` |
| `PA_TICKETS_PERMISSION` | `D_CREATED_AT NOT NULL`, `N_CREATED_BY NOT NULL` | `D_UPDATED_AT NULL`, `N_UPDATED_BY NULL` |
| `PA_TICKETS_ROLE_PERMISSION` | `D_CREATED_AT NOT NULL`, `N_CREATED_BY NOT NULL` | none in the base definition |

This is the source-defined contract. The previous PostgreSQL catalog comparison found
`N_CREATED_BY` missing from role and permission, and `D_CREATED_AT`/`N_CREATED_BY` missing from
role-permission. The current EF rows do not expose role/permission creator/updater fields;
role-permission exposes validity and update fields but not creation metadata. `AdministrationService`
creates custom roles and role-permission grants with the authenticated actor available, but does
not currently set creation metadata.

The 031 draft's backfill is not truthful or stable for a deployment missing those fields:

- Adding `D_VALID_FROM NOT NULL DEFAULT CURRENT_TIMESTAMP` populates existing mappings before the
  subsequent `COALESCE(D_VALID_FROM, D_CREATED_AT, CURRENT_TIMESTAMP)` runs. The existing
  `D_CREATED_AT` therefore cannot supply historical effective dates for those rows.
- `N_UPDATED_BY DEFAULT 0`, role/permission `N_CREATED_BY = 0`, and grant `N_CREATED_BY = 0`
  assume that user ID zero is a valid actor. The repository does not establish that.
- Scope backfill copies the membership's `N_CREATED_BY` into the newly created scope row. That
  person may have created the membership, but did not create the migration-generated scope.

**Required decision:** choose a truthful representation for legacy rows whose historical creation
time/actor cannot be recovered. Alternatives are (a) keep unknown legacy metadata nullable and
record migration provenance separately, (b) approve a specific historical backfill rule and
accept its meaning, or (c) block migration until authoritative source history is available.
Do not fabricate ID zero or copy the membership creator as the creator of a new scope. New
application-created roles/grants can record the authenticated actor and current timestamp.

### Audit actor representation

The canonical schema defines `PA_TICKETS_AUDIT.N_ACTOR_USER_ID` as nullable,
`S_ACTOR_TYPE NOT NULL DEFAULT 'USER'`, and an FK from non-null user actors to
`PA_TICKETS_USER`. In contrast, the previous PostgreSQL preflight found `N_ACTOR_USER_ID` non-null
and no actor FK. The current EF `AuditRow` has a non-nullable `long ActorId` and no actor-type
property. These are three different contracts; none can be silently assumed to be the deployed
one.

No human should be invented for system/migration actions. The source-compatible candidate is
`S_ACTOR_TYPE = 'SYSTEM'` with nullable `N_ACTOR_USER_ID`, but adopting it for PostgreSQL requires
changing that schema and the EF audit row/writers. Other alternatives are an explicitly provisioned
system principal with a real user ID, or a separate actor-identity relation/type. Each has
different FK, access, and audit-retention implications.

**Required decision:** approve one actor model, define which events use it, and specify FK behavior.
Until then, do not add an audit FK or write a placeholder actor. A migration must preserve existing
audit rows and must not retrofit a fabricated user identity.

## Migration dependency and object trace

Repository inspection found no PostgreSQL migration ledger, runner, `__EFMigrationsHistory`, or
other tracked applied-version mechanism. The SQL drafts and catalog shape cannot establish which
files ran. The deployment must first identify its exact baseline and applied versions through an
approved read-only preflight or an external release record; do not infer migration history from
columns alone.

| Draft | Tables/columns and references | Constraints/indexes/sequences | Disposition |
|---|---|---|---|
| `030_five_role_rbac.sql` | Reads/inserts `PA_TICKETS_ROLE` (`N_ROLE_ID`, role code/name/description/system/active); reads `PA_TICKETS_PERMISSION` (permission ID/code/active); inserts `PA_TICKETS_ROLE_PERMISSION` (ID/role/permission); creates `PA_TICKETS_ADMIN_HEAD_DELEGATION` (delegation ID, head/admin user IDs, effective dates, active, created time/actor), referencing `PA_TICKETS_USER`. Requires role and role-permission sequences. Its role and mapping inserts omit canonical `N_CREATED_BY` fields (and mapping `D_CREATED_AT` relies on a default). | Delegation PK `PK_PA_TICKETS_ADMIN_DELEG`; user FKs `FK_PA_TICKETS_AHD_HEAD`, `FK_PA_TICKETS_AHD_ADMIN`; date/active checks `CK_PA_TICKETS_AHD_ACTIVE`, `CK_PA_TICKETS_AHD_DATES`; unique `UK_PA_TICKETS_AHD_START`; index `IX_PA_TICKETS_AHD_EFFECTIVE`; sequence `PA_TICKETS_ADMIN_DELEGATION_SEQ`. | Explicitly superseded by configurable RBAC design; do not execute or use as a prerequisite. |
| `031_configurable_rbac.sql` | Extends `PA_TICKETS_PERMISSION` with `S_MODULE_CODE`, `S_ACTION_CODE`, `S_SCOPE_CLASS`, `S_SECURITY_CLASS`, `N_READ_ONLY`; extends `PA_TICKETS_ROLE_PERMISSION` with `D_VALID_FROM`, `D_VALID_TO`, `N_ACTIVE`, `D_UPDATED_AT`, `N_UPDATED_BY`, and reads/writes creation metadata; creates `PA_TICKETS_USER_ROLE_SCOPE` with membership, department, team, and client IDs plus scope/effective/audit fields; creates `PA_TICKETS_ADMIN_HEAD_DELEGATION`; reads `PA_TICKETS_USER_ROLE`, `PA_TICKETS_ROLE`, `PA_TICKETS_USER`, `PA_TICKETS_DEPARTMENT`, `PA_TICKETS_TEAM`; references `PA_TICKETS_CLIENT`. Uses role, permission, role-permission, role-scope, and delegation sequences. | Canonical role/permission unique indexes `UX_PA_TICKETS_ROLE_CODE_CANONICAL`, `UX_PA_TICKETS_PERMISSION_CODE_CANONICAL`; role-permission date check `CK_PA_TICKETS_RP_DATES`; drops full unique constraint `UK_PA_TICKETS_RP`, replaces it with active-only unique index `UK_PA_TICKETS_RP_ACTIVE`, and adds `IX_PA_TICKETS_RP_EFFECTIVE`; scope PK `PK_PA_TICKETS_ROLE_SCOPE`, FKs `FK_PA_TICKETS_RSC_USER_ROLE`, `FK_PA_TICKETS_RSC_DEPARTMENT`, `FK_PA_TICKETS_RSC_TEAM`, `FK_PA_TICKETS_RSC_CLIENT`, checks `CK_PA_TICKETS_RSC_TYPE`, `CK_PA_TICKETS_RSC_DATES`, `CK_PA_TICKETS_RSC_ACTIVE`, index `IX_PA_TICKETS_RSC_EFFECTIVE`; delegation PK `PK_PA_TICKETS_ADMIN_DELEG`, user FKs `FK_PA_TICKETS_AHD_HEAD`/`FK_PA_TICKETS_AHD_ADMIN`, checks `CK_PA_TICKETS_AHD_ACTIVE`/`CK_PA_TICKETS_AHD_DATES`, unique `UK_PA_TICKETS_AHD_START`, index `IX_PA_TICKETS_AHD_EFFECTIVE`. | Incompatible with the prior observed PostgreSQL base shape. Repair only after the client import, metadata, actor, date-backfill, and legacy-grant decisions are resolved. |
| `032_rbac_independent_approval.sql` | Creates `PA_TICKETS_RBAC_PROPOSAL`, with proposal ID/type/status, target role/permission, initiator, create/expiry times, reason/idempotency/payload/hash, approver/canceller/decision/outcome/correlation/version fields. References `PA_TICKETS_ROLE`, `PA_TICKETS_PERMISSION`, and `PA_TICKETS_USER` for target and actor IDs. | PK `PK_PA_TICKETS_RBAC_PROPOSAL`; checks `CK_PA_TICKETS_RBAC_PROPOSAL_ID`, `_OP`, `_STATUS`, `_DATES`, `_VERSION`; FKs `_ROLE`, `_PERMISSION`, `_INITIATOR`, `_APPROVER`, `_CANCELLER`; unique index `UX_PA_TICKETS_RBAC_PROPOSAL_IDEMPOTENCY`; queue and initiator indexes `IX_PA_TICKETS_RBAC_PROPOSAL_QUEUE`, `IX_PA_TICKETS_RBAC_PROPOSAL_INITIATOR`. | Depends on an approved, repaired 031/base contract. The previous catalog preflight found the proposal relation absent; this does not prove migration history. |

Column-level object references from those drafts:

- **030:** `PA_TICKETS_ROLE` (`N_ROLE_ID`, `S_ROLE_CODE`, `S_ROLE_NAME`,
  `S_DESCRIPTION`, `N_SYSTEM_ROLE`, `N_ACTIVE`); `PA_TICKETS_PERMISSION`
  (`N_PERMISSION_ID`, `S_PERMISSION_CODE`, `N_ACTIVE`); `PA_TICKETS_ROLE_PERMISSION`
  (`N_ROLE_PERMISSION_ID`, `N_ROLE_ID`, `N_PERMISSION_ID`); and
  `PA_TICKETS_ADMIN_HEAD_DELEGATION` (`N_ADMIN_DELEGATION_ID`, `N_ADMIN_HEAD_USER_ID`,
  `N_ADMIN_USER_ID`, `D_VALID_FROM`, `D_VALID_TO`, `N_ACTIVE`, `D_CREATED_AT`, `N_CREATED_BY`).
  The delegation PK uses its ID; its user FKs point both actor-role columns to
  `PA_TICKETS_USER.N_USER_ID`; its date check compares `D_VALID_TO`/`D_VALID_FROM`; its unique key
  is `(N_ADMIN_HEAD_USER_ID, N_ADMIN_USER_ID, D_VALID_FROM)`. Its effective index covers both
  user IDs, `N_ACTIVE`, and the validity dates. It consumes role and role-permission sequences
  without creating them.
- **031:** Role preflight/seeding references `PA_TICKETS_ROLE.N_ROLE_ID`, `S_ROLE_CODE`,
  `S_ROLE_NAME`, `S_DESCRIPTION`, `N_SYSTEM_ROLE`, `N_ACTIVE`, and `N_CREATED_BY`.
  Permission preflight/seeding references `PA_TICKETS_PERMISSION.N_PERMISSION_ID`,
  `S_PERMISSION_CODE`, `S_PERMISSION_NAME`, `S_DESCRIPTION`, `S_MODULE_CODE`, `S_ACTION_CODE`,
  `S_SCOPE_CLASS`, `S_SECURITY_CLASS`, `N_READ_ONLY`, `N_ACTIVE`, and `N_CREATED_BY`.
  Role-permission backfill/seeding/indexes reference `N_ROLE_PERMISSION_ID`, `N_ROLE_ID`,
  `N_PERMISSION_ID`, `D_CREATED_AT`, `N_CREATED_BY`, `D_VALID_FROM`, `D_VALID_TO`, `N_ACTIVE`,
  `D_UPDATED_AT`, and `N_UPDATED_BY`.
  The scope table contains `N_ROLE_SCOPE_ID`, `N_USER_ROLE_ID`, `S_SCOPE_TYPE`,
  `N_ORGANIZATION_ID`, `N_DEPARTMENT_ID`, `N_TEAM_ID`, `N_CLIENT_ID`, `S_RESOURCE_TYPE`,
  `S_RESOURCE_ID`, `D_VALID_FROM`, `D_VALID_TO`, `N_ACTIVE`, `D_CREATED_AT`, and `N_CREATED_BY`.
  Its FKs are `N_USER_ROLE_ID`â†’`PA_TICKETS_USER_ROLE.N_USER_ROLE_ID`,
  `N_DEPARTMENT_ID`â†’`PA_TICKETS_DEPARTMENT.N_DEPARTMENT_ID`,
  `N_TEAM_ID`â†’`PA_TICKETS_TEAM.N_TEAM_ID`, and `N_CLIENT_ID`â†’
  `PA_TICKETS_CLIENT.N_CLIENT_ID`; `N_ORGANIZATION_ID` and resource identifiers have no FK in
  this draft. Scope type/date/active checks cover `S_SCOPE_TYPE`, the validity dates, and
  `N_ACTIVE`. The scope effective index is `(N_USER_ROLE_ID, S_SCOPE_TYPE, N_ACTIVE,
  D_VALID_FROM, D_VALID_TO)`.
  Delegation uses the 030 columns and keys. Scope backfill reads
  `PA_TICKETS_USER_ROLE` (`N_USER_ROLE_ID`, `N_ROLE_ID`, `N_USER_ID`, `D_VALID_FROM`,
  `D_VALID_TO`, `N_ACTIVE`, `N_CREATED_BY`), joins role code via `PA_TICKETS_ROLE`, and reads
  `PA_TICKETS_USER.N_USER_ID`, `N_DEPARTMENT_ID`, and `N_TEAM_ID`. It consumes
  `PA_TICKETS_ROLE_SEQ`, `PA_TICKETS_PERMISSION_SEQ`, `PA_TICKETS_ROLE_PERMISSION_SEQ`,
  `PA_TICKETS_ROLE_SCOPE_SEQ`, and `PA_TICKETS_ADMIN_DELEGATION_SEQ`.
- **032:** `PA_TICKETS_RBAC_PROPOSAL` columns are `S_PROPOSAL_ID`, `S_OPERATION_TYPE`,
  `S_STATUS`, `N_TARGET_ROLE_ID`, `N_TARGET_PERMISSION_ID`, `N_INITIATOR_USER_ID`,
  `D_CREATED_AT`, `D_EXPIRES_AT`, `S_REASON`, `S_IDEMPOTENCY_KEY`, `S_PAYLOAD_JSON`,
  `S_PAYLOAD_SHA256`, `N_APPROVER_USER_ID`, `N_CANCELLED_BY_USER_ID`, `D_DECISION_AT`,
  `S_DECISION_REASON`, `S_APPLICATION_OUTCOME`, `S_CORRELATION_ID`, and `N_ROW_VERSION`.
  Its five FKs point to `PA_TICKETS_ROLE.N_ROLE_ID`, `PA_TICKETS_PERMISSION.N_PERMISSION_ID`,
  and `PA_TICKETS_USER.N_USER_ID` for initiator, approver, and canceller. Checks constrain the
  lowercase 32-character UUID string, operation type, status set, expiry-after-create, and positive
  version. The unique idempotency index is `(N_INITIATOR_USER_ID, S_IDEMPOTENCY_KEY)`; the queue
  index is `(S_STATUS, D_EXPIRES_AT, D_CREATED_AT)`; the initiator index is
  `(N_INITIATOR_USER_ID, D_CREATED_AT)`. It creates no sequence.

The existing role IDs, permission IDs, user-role memberships, role-permission rows, user-client
rows, and audit records must remain intact. The 031 replacement must preflight case-folded role/
permission collisions and canonical aliases, preserve existing IDs, and stop rather than rename or
merge any identity. Before replacing `UK_PA_TICKETS_RP`, inspect duplicate inactive role-permission
pairs and preserve every row; no deletion or compaction is authorized.

## Review-draft repair shape

This is a draft sequence, not executable SQL:

1. Verify and record the baseline/release version outside the migration; assert required base
   tables, columns, sequences, and key constraints before any DDL. Do not use 030.
2. Preflight role/permission code collisions, sequence collisions, existing grant duplicates,
   client IDs in user-client/ticket/announcement data, and all intended FK orphans. Stop on any
   unmatched client identity or metadata that cannot be backfilled truthfully.
3. Use the canonical client column contract only after an approved PostgreSQL import/ID policy.
   Add the scope-to-client FK and any missing user-client/ticket/announcement FKs only after a
   non-destructive orphan check. No client IDs or catalogue values are synthesized.
4. Add RBAC permission/effective-grant columns and scope/delegation structures with the reviewed
   constraints/indexes. For legacy `D_VALID_FROM`, use a verified historical value when one exists;
   otherwise require an approved policy instead of relying on a `NOT NULL DEFAULT CURRENT_TIMESTAMP`
   that masks the old value. Preserve all inactive historical grant rows when installing the active
   unique index.
5. Do not insert zero or another guessed `N_CREATED_BY`/`N_UPDATED_BY`. Use the approved system
   actor/provenance design for migration-created seed and scope rows, or block those inserts until
   it is approved. Preserve built-in role identities and allocate any new role/permission/mapping
   identifiers from approved PostgreSQL sequences without overwriting existing IDs.
6. Apply the approval proposal DDL only after 031 has completed and the EF contract is aligned.
   Retain proposal FK delete restrictions and all idempotency, queue, and initiator indexes.

### Review-only SQL fragment for the historical effective-date gate

This PostgreSQL fragment illustrates a fail-closed replacement for the current
`D_VALID_FROM NOT NULL DEFAULT CURRENT_TIMESTAMP` behavior. It is **not** a complete migration and
must not be executed until the metadata/nullability contract is approved:

```sql
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "D_CREATED_AT" timestamp with time zone;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "N_CREATED_BY" bigint;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "D_VALID_FROM" timestamp with time zone;

UPDATE "PA_TICKETS_ROLE_PERMISSION"
   SET "D_VALID_FROM" = "D_CREATED_AT"
 WHERE "D_VALID_FROM" IS NULL
   AND "D_CREATED_AT" IS NOT NULL;

DO $effective_date_gate$
BEGIN
    IF EXISTS (
        SELECT 1 FROM "PA_TICKETS_ROLE_PERMISSION"
         WHERE "D_VALID_FROM" IS NULL
    ) THEN
        RAISE EXCEPTION
            'RBAC repair blocked: historical role-permission effective dates require an approved source';
    END IF;
END
$effective_date_gate$;
```

The fragment intentionally neither fabricates a timestamp nor sets actor ID zero. It does not
solve whether unknown `D_CREATED_AT`/`N_CREATED_BY` values should remain nullable, be supplied by
an approved mapping, or block deployment. If a prior run already materialized migration-time
`D_VALID_FROM` values, that timestamp is not proof of the original grant time; recovery requires
an authoritative history source or an explicit policy decision.

## EF Core review draft

The following row shape is a review proposal, not an applied runtime mapping. The client catalog
and generated-key policy must be decided before registering it in `TicketsDbContext`:

```csharp
[Table("PA_TICKETS_CLIENT")]
public sealed class AdministrationClientRow
{
    [Key, Column("N_CLIENT_ID")] public long Id { get; set; }
    [Column("S_CLIENT_CODE")] public string Code { get; set; } = "";
    [Column("S_CLIENT_NAME")] public string Name { get; set; } = "";
    [Column("S_DESCRIPTION")] public string? Description { get; set; }
    [Column("S_STATUS")] public string Status { get; set; } = "ACTIVE";
    [Column("S_CONTACT_NAME")] public string? ContactName { get; set; }
    [Column("S_CONTACT_EMAIL")] public string? ContactEmail { get; set; }
    [Column("S_CONTACT_PHONE")] public string? ContactPhone { get; set; }
    [Column("S_CLASSIFICATION")] public string? Classification { get; set; }
    [Column("D_CREATED_AT")] public DateTimeOffset CreatedAt { get; set; }
    [Column("N_CREATED_BY")] public long CreatedBy { get; set; }
    [Column("D_UPDATED_AT")] public DateTimeOffset? UpdatedAt { get; set; }
    [Column("N_UPDATED_BY")] public long? UpdatedBy { get; set; }
}
```

The role and permission review delta is `CreatedAt`/`CreatedBy` plus nullable `UpdatedAt`/
`UpdatedBy`; role-permission needs `CreatedAt`/`CreatedBy` in addition to its current validity,
active, and update fields. Whether legacy creator fields are nullable until backfilled or remain
non-null per the static schema is a required contract decision. The model and runtime write paths
must be changed together; adding non-null properties alone would make old rows unreadable or new
writes incomplete.

Register a client `DbSet` and explicit model relationships only after the agreed PG schema and
deletion policy are known. Map client FKs for user-client links, tickets, announcements, and role
scopes only after the import/preservation checks.
`AdministrationService.AssignClientOnceAsync` currently checks only that the user is active; it
should validate that the referenced client exists and is eligible before inserting a membership,
with a deterministic missing/inactive-client test, once client lifecycle semantics are approved.

Align the role and permission rows with canonical create/update fields, and align
`AdministrationRolePermissionRow` with `D_CREATED_AT`/`N_CREATED_BY` plus the reviewed effective
and update metadata. New custom-role and role-permission writes must set authenticated actor and
time. Existing role metadata updates must set updater actor/time. Permission catalog seeding must
follow the approved seed/system provenance rule. Model tests should assert property-to-column,
nullability, keys, indexes, and FK metadata without connecting to a provider.

The audit model needs an explicit actor-type representation if `SYSTEM`/null-actor is approved.
That would require nullable `ActorId`, mapped `ActorType`, and a review of all audit writers and
provider constraints; do not make that runtime change before the actor contract decision.

The proposal row and 032 indexes/FKs/checks already have corresponding EF mappings for the target
role, permission, and user relationships. Provider-neutral model metadata tests are still needed
to prove the relational model matches the final reviewed DDL.

## Required approval and validation before execution

1. Approve the PostgreSQL client catalog source, ID-generation/import mapping, FK set, and
   retirement/delete semantics.
2. Approve the treatment of missing historical creator/effective timestamps and migration-created
   scope provenance; approve whether actor metadata can be nullable for unknown historical rows.
3. Approve the system audit actor representation and whether the PostgreSQL audit actor FK should
   be added or reconciled with retained rows.
4. Establish the exact baseline and migration-history/release-record procedure; confirm whether
   028/029 fields already exist, do not run 030, then order repaired 031 before 032.
5. Review the final SQL and EF model together, then require static contract tests, read-only
   preflight, and an explicitly approved disposable-provider rehearsal with ID/count/hash
   preservation assertions, rollback/recovery, concurrency, and failure-injection coverage.

Until those decisions and separate database authorization exist, 031/032 stay blocked and
unexecuted. No production-readiness or end-to-end acceptance claim follows from this draft.


## Current read-only PostgreSQL catalog evidence (2026-10-15)

A catalog-only transaction was run against the configured PostgreSQL connection. It reported database
`pa_tickets_h_verify`; the shared `pa_tickets_test` database was not accessed. The transaction
queried only `information_schema` and PostgreSQL catalogs; it read no application rows and made no
schema or data changes.

| Observed object | Actual catalog shape | Consequence |
|---|---|---|
| `PA_TICKETS_ROLE` | Exists with bigint ID sequence, code/name/description, system and active flags. No creator, creation-time, or update metadata columns were returned. | 031’s built-in role inserts reference `N_CREATED_BY`; its role model contract is not met. |
| `PA_TICKETS_PERMISSION` | Exists with bigint ID sequence, code/name/description and active flag. Module/action/scope/security/read-only metadata and creator/update fields are absent. | The authorization service and 031 permission catalogue cannot run against this baseline. |
| `PA_TICKETS_ROLE_PERMISSION` | Exists with bigint mapping ID sequence and role/permission IDs only. No creation actor/time, effective dates, active flag, or update metadata. | The 031 backfill has no historical creation timestamp or actor source in this database; its `CURRENT_TIMESTAMP` and actor ID zero values are fabricated history. |
| `PA_TICKETS_USER_ROLE` | Exists with effective dates, active flag and creation actor/time. | Existing memberships can supply current identity and dates, but do not establish when a newly synthesized scope was created. |
| `PA_TICKETS_USER_CLIENT` | Exists with client ID, effective dates, active flag and creation actor/time. | Existing client IDs must be preserved and mapped before introducing a client FK. |
| `PA_TICKETS_CLIENT` | Absent. | Client catalogue source and ID-allocation policy remain unresolved; a scope FK cannot safely be added. |
| Role scopes, Admin Head delegations, RBAC proposals | All three relations are absent. | 031/032 structures are not present and must not be presumed applied. |
| `PA_TICKETS_AUDIT` | Actor ID is non-nullable; no actor-type column is present. | This differs from the source schema’s nullable user actor plus actor type; system/migration audit representation needs an explicit policy. |
| RBAC sequences | Role, permission, role-permission, user-role and user-client sequences exist. No role-scope or delegation sequences were returned. | Any new sequence allocation must be introduced only in an approved migration after the applied baseline is established. |
| Migration ledger | No table matching the migration-ledger catalog search was found. | Applied migration history remains unknown; release records or a separately approved preflight are required. |

This is evidence for the configured isolated database only. It does not prove the production or any
other deployment has the same schema. Existing role IDs, membership rows, permission mappings and
audit history were not selected or modified. Migration 030/031/032 remain unexecuted. The required
data-history, actor-model, client-import, and migration-baseline decisions in this document remain
deployment blockers.



## Authorization policy gap and implemented source change

The role-code resource bypass has been removed from the common authorization evaluator. Permissions marked `NONE` require an explicit effective organization scope on that membership; all other permissions pass their declared scope class through the regular membership-scope evaluator, where organization scope is checked as a resource scope. Search and reporting apply the same permission and scope checks before returning organization-wide results. No account receives this scope automatically. Database deployment and scope assignment remain blocked pending the reviewed migration and independent approval workflow.

## User-directed decisions and reconciled data (2026-10-15)

The design decisions below supersede earlier open questions in this draft. The configured verification
connection was queried in read-only transactions and identified `pa_tickets_h_verify`; it did not
connect to `pa_tickets_test`. The earlier statement that this review did not read application rows
is superseded by the aggregate/reference-only results below. No application rows were changed.

### Client and organization identity

The application is single-organization. Do not create a canonical organization row or reinterpret a
client ID as an organization ID. `ORGANIZATION` scope with `N_ORGANIZATION_ID IS NULL` is the
single-organization sentinel. It has stable meaning only together with the single-organization
application invariant; a future multi-organization change must replace it through a separately
reviewed migration and explicit mapping. At authorization time it matches resources with a null
organization ID; a non-null resource organization ID requires exact equality and fails closed.

The actual database has no `PA_TICKETS_CLIENT` table. There are 82 ticket rows and none has a
non-null `N_CLIENT_ID`. `PA_TICKETS_USER_CLIENT` has zero rows. `PA_TICKETS_SLA_POLICY` has 113
rows, five of which reference client ID `872002`; the other 108 have null client IDs. Preserve
`872002` exactly. No client name, code, or organization identity can be inferred from this row.
Before any client FK or client catalogue DDL, the operator must supply an authoritative catalogue
entry/mapping for `872002`, or a verified source proving what it references. Do not allocate a new
ID, rewrite `872002`, or add a FK until the target row exists and preflight confirms no orphans.
The catalogue should use the established ID unchanged, a primary key on `N_CLIENT_ID`, unique
canonical client code, restrictive FK deletion, and soft retirement; exact non-key columns and
sequence policy must follow the authoritative source, not be synthesized from this application
schema alone.

### Actors and effective dates

The database has 2,967 audit rows. All have a non-null actor ID; all currently join to an existing
`PA_TICKETS_USER` row. The deployed audit actor column is `NOT NULL` and has no actor FK. Preserve
all values. The model's explicit representation for a future imported event whose actor is
unavailable is `S_ACTOR_TYPE='LEGACY_UNKNOWN'` with `N_ACTOR_USER_ID=NULL`; `USER` actors require a
non-null ID and the user FK, while `SYSTEM` uses a separately identified system event type and
must not borrow a human ID. This requires an additive actor-type column, nullable EF actor ID, a
check constraint, and a nullable FK (`ON DELETE RESTRICT`); it cannot be implemented by inserting
an invented user or by changing existing actor values. No migration-generated audit event is
needed for this repair. The current schema change remains blocked until all audit writers,
readers, and provider contracts are updated together.

All 21 existing user-role memberships have non-null start dates, none start in the future, and
none has an invalid end-before-start interval. The 44 role-permission rows have only role and
permission IDs: there is no source start date, creator, or active flag. Their historical effective
dates are unknown. Preserve their IDs and pairs, and do not make them active by assigning the
migration time. The repair must retain the original rows and provenance, mark unknown-date grants
non-authorizing (or leave them in an immutable legacy staging relation), and require a separately
reviewed reconciliation for each grant before inserting an effective grant with a truthful start
date. This fail-closed rule must be reflected in nullable date model/query semantics before DDL
that permits null effective starts is deployed.

### Actual catalog and migration history

The database contains the role, permission, role-permission, user-role and user-client sequences,
plus the existing unrelated application sequences. It has no role-scope or Admin Head delegation
sequence. Relevant existing FKs include role-permission to role and permission, user-role to user
and role, and user-client to user. There is no client FK on ticket, SLA policy, or user-client; no
audit actor FK; and no matching migration ledger table. The observed catalog therefore cannot
identify which numbered SQL files were applied. The deployment baseline must be established from an
approved release record and an exact read-only schema fingerprint before choosing migration IDs or
replaying any file.

### Super Admin scope implementation

The role-code shortcut that returned `true` for a `SUPER_ADMIN` grant has been removed from the
central authorization evaluator. Organization resources now pass through the normal permission
metadata/grant and membership-scope checks. Organization matching is exact; the single-org sentinel
is `ORGANIZATION` plus null organization ID. A shared effective-scope query requires an active
Super Admin membership and active, date-effective organization scope. Ticket search, reports, KPIs,
and auditor ticket selection now use that explicit scope and the common permission check before
returning organization-wide results. No account receives this scope automatically. The scope
validator accepts it only for a Super Admin membership and rejects a non-null organization ID.
Scope assignment remains fail-closed because the existing API has no independently approved
organization-scope proposal operation. Sensitive admin writes continue to use their existing
actor-attributed audit events; adding a new scope grant workflow must add corresponding approval
and audit coverage before it can write.

### Exact deployment operation plan (not authorization to execute)

1. **Read-only preflight on the named target:** record database identity, schema fingerprint,
   release baseline, relation/column/sequence definitions, all constraints/indexes, role and
   permission code collisions, role-permission duplicate pairs, client-ID references, audit actor
   orphans, and membership/grant date null/invalid counts. Export counts and stable ID/pair
   manifests for `PA_TICKETS_ROLE`, `PA_TICKETS_PERMISSION`, `PA_TICKETS_ROLE_PERMISSION`,
   `PA_TICKETS_USER_ROLE`, `PA_TICKETS_USER_CLIENT`, `PA_TICKETS_AUDIT`, tickets and SLA policies.
   Abort if baseline/history is ambiguous or manifests cannot be preserved.
2. **Resolve data contracts before DDL:** verify the authoritative source for client `872002`;
   reconcile all 44 role-permission grants using an authoritative effective-date source or approve
   that they remain inactive pending individual reconciliation; approve the actor-type/null actor
   compatibility change; confirm audit FK behavior; and approve the organization-scope sentinel.
   If any item is unresolved, stop before schema changes.
3. **Prepare incremental migrations from the verified baseline:** (a) add/update EF-compatible
   nullable legacy provenance/effective-date columns and permission metadata without defaults that
   fabricate history; (b) add organization-capable user-role scope table, sequence, exact checks,
   role-membership FK and effective lookup index, with no automatic scope rows; (c) add delegation
   and proposal relations only after checking current service/schema contract; (d) add the client
   catalogue entry for ID `872002` from the approved source and then restrictive FKs after orphan
   checks; (e) add the audit actor type/check/nullable user FK only after all writers and readers
   support `LEGACY_UNKNOWN`. Do not execute `030_five_role_rbac.sql`; treat `031` and `032` as
   drafts to be replaced by these ordered deltas, not as replayable migrations.
4. **Apply each reviewed migration transactionally to the explicitly named target** only after
   separate approval states the target, migration file hashes, and operation. Do not seed accounts,
   permissions, role scopes, client names, or audit actors as an incidental part of schema repair.
5. **Post-migration assertions:** compare stable IDs and row counts to the preflight manifests;
   verify all old role/permission/grant/membership/audit IDs and values are preserved; verify
   `872002` is unchanged and every client FK has zero orphans; verify unknown-date grants are
   non-authorizing and no Super Admin has an organization scope unless explicitly approved; verify
   the audit actor-type check/FK and legacy rows; compare schema fingerprint with the reviewed
   migration; run provider contract tests and only then separately approved authenticated DB E2E.
6. **Rollback/recovery:** before execution, take a target-consistent schema/data backup and retain
   the immutable preflight manifests. For additive DDL, rollback drops only newly added constraints,
   indexes, columns, sequences, and empty new tables in reverse dependency order. If any new rows
   have been written, do not drop their tables or delete rows; disable the release and restore from
   the approved backup or use a separately reviewed forward repair. Never reverse by deleting or
   renumbering legacy identities. DDL/data backfill must be one migration transaction where
   PostgreSQL permits it; abort and rollback on any assertion failure.

No migration or provisioning was executed. An accidentally broad `dotnet test` invocation started the PostgreSQL integration suite against `pa_tickets_h_verify` and inserted fixture rows before failing on the missing permission metadata column; the run was stopped. Read-only follow-up observed ticket count 82 to 92 and audit count 2,967 to 2,982. Newly observed ticket IDs were 820-822, 829-834, and 840; audit rows include ticket, problem, attachment, and evidence fixture events. No cleanup was run. The end-to-end acceptance matrix remains 8 PASS / 3 PARTIAL / 13 BLOCKED; no database-backed E2E success is claimed.









## Incident containment and test-target control (2026-10-16)

The prior incident occurred because PostgreSQL test methods only required a connection-string environment variable. A normal full-project invocation discovered those tests and `EnsureConfigured()` accepted `pa_tickets_h_verify`; fixture code then opened providers and wrote before the schema mismatch stopped execution. The database name was never an approval boundary. The evidence remains bounded: read-only follow-up identified 10 additional tickets and 15 audit rows, but dependent rows, failed attempts, sequence advancement, and any writes before the observed rows are not fully reconstructed. No cleanup is authorized or attempted.

A source-level guard now runs at every PostgreSQL test entry point. It requires an explicit write opt-in token and an independently supplied host/port/database/user descriptor that exactly matches the parsed connection string. It fails before provider construction and never connects to validate the target. The guard does not infer approval from a database name, credentials, or test discovery, and it does not provision, migrate, seed, fixture, or clean. Four database-independent guard tests cover missing configuration, missing opt-in, target mismatch, and credential-free descriptor output.

The approved workflow for a future provider run is: approve the exact disposable target and operation; independently verify host, port, database, user, environment, and role; set the process-only opt-in, target descriptor, and connection string; run only the named PostgreSQL tests; and separately approve provisioning/migration/fixture/cleanup steps. A database name is an identifier, not proof of target ownership or disposability. The current incident database remains outside any cleanup scope.

## Explicit migration-readiness matrix (2026-10-16)

| Area | Current evidence | Exact unresolved decision | Additive migration proposal | Preservation and preflight | Post-migration assertion / rollback constraint | Required approval and validating tests |
|---|---|---|---|---|---|---|
| Client `872002` | Five SLA policy rows reference it; tickets and user-client memberships have no client IDs; client table is absent | Identify the authoritative client source and intended semantics | Add the client catalogue row using ID `872002` only after source approval; add restrictive FKs only afterward | Export all client references; prove the source row exists; abort on orphan or ID mismatch | Zero FK orphans; ID remains `872002`; never delete/remap legacy references; restore backup or use reviewed forward repair if dependent rows exist | Client-source/ID/FK approval; provider model and orphan-check tests after target approval |
| Role-permission dates | 44 rows have role/permission IDs only and no effective dates or creator provenance | Per-row historical effective-date source, or explicit inactive legacy policy | Add nullable provenance/effective metadata and an immutable legacy reconciliation path; do not default to current time | Preserve IDs/pairs and export a manifest; mark unknown-date rows non-authorizing until reconciled | Unknown rows cannot authorize; counts/pairs/hash manifest unchanged; no destructive rollback of legacy rows | Historical-data owner approval; fail-closed authorization and migration rehearsal tests |
| Audit actors | 2,967 legacy rows have non-null actors resolving to users; deployed actor column is non-null with no FK/type | Compatibility contract for future unknown/system actors | Add actor type and nullable user ID only after writers/readers support `LEGACY_UNKNOWN`; retain existing values | Check all actor IDs and FK orphans; never rewrite historical actors or invent a user | Check constraint/FK passes and legacy hashes/values remain unchanged; restore/forward repair if new actor rows exist | Audit/security approval; provider writer, reader, FK, and chain-integrity tests |
| Migration baseline | No migration ledger table; required 031/032 relations/columns are absent in the observed database | Approved release baseline and exact applied-version evidence | Prepare new ordered deltas from a verified fingerprint; do not replay 030/031/032 blindly | Capture relation/column/constraint/sequence fingerprints and stable ID manifests | Fingerprint matches reviewed migration; rollback only additive empty objects, otherwise restore/forward repair | Release/database owner approval; read-only preflight and disposable rehearsal |
| Organization scope | Single-organization app; null-ID `ORGANIZATION` sentinel implemented in source; no scope table deployed | Confirm sentinel remains valid until any future multi-organization design | Add scope table/check/index with `ORGANIZATION` + null organization ID; no automatic rows or positive organization ID | Verify all scope/resource matching paths and no positive ID is inferred | Scope checks exact/null-safe; no account gains scope during migration; drop only empty new objects on rollback | Security owner and independent-approval owner; unit tests (29 passed), then provider authorization tests |
| Super Admin bypass | Role-code short-circuit removed; search/report/KPI/auditor paths require explicit scope and permission | Independent approval operation for scope grants | Extend proposal/workflow only after schema and two-person policy review | Search source for role-code bypasses; verify grants and active/effective scope rows independently | No unscoped organization-wide result; no automatic scope grant; restore/forward repair if new grants exist | Security and governance approval; provider/API scope-isolation tests |
| Test target safety | Previous helper accepted credentials alone; accidental run wrote before schema failure | Exact disposable target and independent identity record | No database migration; guard is source-only and pre-provider | Require opt-in token plus exact host/port/database/user descriptor; never log credentials | Guard fails before provider setup; no cleanup inferred from test failure | Test-owner approval; four pure guard tests plus scope tests passed in the safe selection |

## Reviewable migration set (2026-10-16)

The superseded drafts `030_five_role_rbac.sql`, `031_configurable_rbac.sql`, and `032_rbac_independent_approval.sql` remain in the repository for review history and must not be replayed. They assume relations/columns that are absent from the inspected PostgreSQL catalog and include unsafe historical-date, scope, or permission-grant backfills.

The replacement review set is dependency ordered:

| File | Purpose | Data effect | Gate |
|---|---|---|---|
| `033_rbac_migration_ledger.sql` | Add an explicit migration ledger and target/fingerprint fields | Additive DDL only; it does not mark history applied | Verified target identity, approved baseline and fingerprint |
| `034_rbac_permission_metadata.sql` | Add module/action/scope/security metadata and provenance-aware effective fields | No historical dates or grants are backfilled; unknown status remains fail-closed | Application release denies `UNKNOWN`; catalog preflight passes |
| `035_rbac_scopes_and_delegation.sql` | Add explicit membership scopes and Admin Head delegation | No scope, client, or delegation rows are inserted; organization scope is NULL organization ID | Parent relations and single-org sentinel approved |
| `036_rbac_proposals.sql` | Add durable two-person proposal records for permission/scope changes | No proposals, grants, or scopes are inserted | Independent approval workflow and parent schema approved |
| `037_builtin_role_provisioning.sql` | Provision the five built-in role identities idempotently | Inserts only missing role rows; preserves existing IDs; inserts no grants or scopes | Explicit role-provisioning session approval |
| `038_rbac_audit_actor_compatibility_BLOCKED.sql` | Safety gate for unresolved historical actor design | Always aborts; performs no schema change | Explicit `LEGACY_UNKNOWN`/`SYSTEM` actor contract and writer/reader release |

No client row for `872002`, role-permission effective date, audit actor, membership, scope, delegation, proposal, permission grant, or account is invented by these drafts. Client-dependent foreign keys and all historical reconciliation remain blocked until their source decisions are approved.

### Execution order and assertions

After a read-only target preflight, approved execution order is 033, 034, 035, 036, and then 037. File 038 must remain blocked until its design decision is resolved. Each transaction must be preceded by an approved backup/snapshot and immutable manifests of role, permission, role-permission, membership, client/SLA references, audit actors, and schema fingerprints. Post-migration checks must prove stable IDs and pair mappings are preserved, `UNKNOWN` grants cannot authorize, no organization scope was inserted, all five role codes exist exactly once, and no permission grant was added by 037.

Rollback is limited to dropping newly added empty objects and constraints in reverse order. Once new rows exist, do not delete them to simulate rollback; disable the release and use an approved backup restore or forward repair. The migration ledger must record the exact file SHA-256 and target fingerprint only after successful application.

### Per-file operational review

* **033 ledger:** `CREATE TABLE/INDEX IF NOT EXISTS` is additive and rerunnable. PostgreSQL takes relation/index definition locks; run during a controlled deployment window. Rollback may remove the new index/table only while empty and before any ledger evidence is recorded. The ledger row itself must be written by the approved runner only after the migration and fingerprint assertions succeed.
* **034 permission metadata:** `ADD COLUMN IF NOT EXISTS`, checks, and the lookup index are additive. The preflight aborts when parent relations or key columns are absent; the post-check rejects partial module/action metadata. It intentionally leaves historical dates unknown and requires an application release that denies `UNKNOWN` before activation. Do not roll back by deleting or rewriting legacy role-permission rows; remove only empty new indexes/columns after a reviewed release rollback.
* **035 scopes/delegation:** New sequences, tables, foreign keys, checks, and indexes are idempotent and insert no rows. The organization sentinel is structurally constrained to `NULL` organization ID. Client constraints are deferred until `872002` semantics are resolved. Rollback is safe only for empty new relations; any created scope/delegation requires backup restoration or forward repair.
* **036 proposals:** The proposal table and indexes are additive and idempotent. The operation/target checks prevent a permission proposal from silently becoming a scope proposal. No proposal is generated by the migration. Once proposals exist, rollback must preserve them and use a reviewed forward state transition or backup restore.
* **037 built-in roles:** The session setting is an explicit execution gate. Existing role IDs are preserved; only missing canonical codes consume sequence values. The migration never inserts permissions, memberships, scopes, users, or grants. It must run after 034 and only after the role seed manifest is approved. If a newly inserted role is referenced, do not delete it during rollback; disable the release and use forward repair or restore.
* **038 audit actors:** This file intentionally aborts before DDL. It remains a hard dependency blocker until the actor-type/nullability/FK contract and all writers/readers are approved together.

All files are transactional drafts. The deployment runner must lock the target to one migration worker, record file hashes and the preflight fingerprint, and stop on any assertion failure. No file is self-authorizing based on table existence or a database name.

## Compatibility review correction (2026-10-16)

Static comparison against `RbacProposalRow`, `AdministrationService`, `SensitiveRbacApprovalPolicy`, and `AdministrationController` found that organization-scope proposals are not implemented: `AssignRoleScopeAsync` rejects scope grants and there is no scope-proposal endpoint or model. Migration 036 therefore now models only the implemented `ROLE_PERMISSION_GRANT` operation and keeps `N_TARGET_PERMISSION_ID` non-null to match EF materialization. Organization-scope independent approval remains a separate BLOCKED design item and must not be represented as an executable migration operation until the application contract exists.

## Fail-closed effective-date implementation (2026-10-16)

The application contract is now explicit: `AdministrationRolePermissionRow.ValidFrom` is nullable, `EffectiveStatus` is one of `UNKNOWN`, `ACTIVE`, `EXPIRED`, or `REVOKED`, and `Active` remains a non-null boolean. A grant authorizes only when `Active=true`, `EffectiveStatus='ACTIVE'`, `ValidFrom` is present, `ValidFrom <= now`, and `ValidTo` is null or strictly later than `now`. Comparisons use `DateTimeOffset` instants (UTC from `TimeProvider`). Unknown, null-start, future, expired, revoked, and inactive rows cannot authorize.

New grants created by the administration service and approval application explicitly set `EffectiveStatus='ACTIVE'` and a current UTC start. Migration 034 defaults newly introduced activity to `false` and status to `UNKNOWN`; it does not invent timestamps or activate historical rows. The authorization and local-authentication SQL predicates repeat the same conditions so null SQL values cannot pass through a fallback branch. Provider translation and materialization against a real PostgreSQL target remain unverified.

The provider-independent helper and regression tests cover unknown, valid, expired, future, start-inclusive, end-exclusive, revoked, and offset-equivalent instants. The focused run passed 52 tests. This does not replace provider-backed SQL verification.

## Decision matrix status

| Decision | Status | Repository evidence | Required next evidence/approval | Dependent work |
|---|---|---|---|---|
| Client `872002` | BLOCKED | Only five SLA policy references were identified; source does not establish authoritative client identity or semantics | Authoritative client record, owner decision, stable-ID preservation and FK mapping | Client FKs, client-scope migration and E2E |
| 44 unknown role-permission dates | RESOLVED FROM REPOSITORY EVIDENCE for fail-closed behavior; reconciliation BLOCKED | Rows lack truthful dates/provenance; source now represents null/status explicitly and denies unknown rows | Per-row evidence and approved reconciliation actor/workflow; no date fabrication | Migration 034 activation, effective-grant E2E |
| Historical audit actors | BLOCKED | Audit entity uses non-null `long ActorId`; writers pass authenticated server actor; legacy unknown/system contract is absent | Approved actor type/nullability/FK and reader/writer migration contract | Migration 038 and actor-preservation E2E |
| Migration baseline | PROPOSED FOR APPROVAL | Repository has numbered drafts but no trustworthy deployed ledger | Read-only target fingerprint, release record, file hashes and stable-ID manifests | Any database migration |
| Organization-wide scope approval | BLOCKED | Explicit scope validator exists; direct scope assignment throws; proposal API only supports permission grants | Implement and test scope proposal/approval, expiry, replay, audit and atomic activation | Organization-scope E2E and scope migration activation |

## Effective-grant compatibility audit (2026-10-16)

Reviewed authorization paths:

- `DatabaseAuthorizationService.HasPermissionAsync`: requires active membership/role, active permission metadata, `rolePermission.Active`, `EffectiveStatus='ACTIVE'`, known start, inclusive start and exclusive end before scope evaluation.
- `LocalAuthenticationService`: includes the same grant conditions when deriving authenticated role permissions.
- `AdministrationService` single, batch, proposal-snapshot, and approval-application paths: effective snapshots include only ACTIVE/known-date grants; new rows explicitly set ACTIVE and current UTC start.
- `ListRolePermissionsAsync`: returns nullable starts and status so unknown rows remain visible for reconciliation without being treated as active.
- Scope, client, delegation, reporting, and membership predicates do not authorize role-permission rows directly and retain their existing time semantics.

The shared provider-independent helper is used to define and test the same boundary semantics: start is inclusive, expiry is exclusive, all comparisons use `DateTimeOffset`, and null/unknown status is false. Query predicates remain explicit LINQ expressions so EF can translate them; actual PostgreSQL translation and nullable `timestamptz` materialization remain unverified.

Static EF conclusions: nullable `DateTimeOffset` follows provider conventions for PostgreSQL `timestamptz`; `EffectiveStatus` is a string matching migration 034; `Active` remains non-null and uses the existing Oracle integer conversion. The PostgreSQL source migration now normalizes only null activity flags to false and leaves dates untouched. The same new column is not present in the checked-in Oracle schema, so any Oracle deployment requires an equivalent reviewed schema change before this source contract can be used there.

No alternate role-permission path was found that omits the ACTIVE/status/known-start checks. Provider SQL behavior, legacy-row materialization, and cross-provider schema compatibility remain unverified.

## Final static provider/readiness review (2026-10-16)

### Oracle/PostgreSQL effective-grant parity

The runtime has explicit provider selection in `DependencyInjection.AddInfrastructure`: `Database:Provider` must be `PostgreSQL` or `Oracle`, EF uses `UseNpgsql` or `UseOracle`, and provider-specific capability/sequence/transaction adapters are registered. This proves configuration support exists in source; it does not prove either deployed schema is compatible or that both providers execute the authorization queries successfully.

| Field | Oracle checked-in baseline (`database/schema/001_PA_TICKETS_SCHEMA.sql`) | PostgreSQL checked-in test slice (`tests/scripts/postgresql-slice1-schema.sql`) | EF/application contract | Required reviewed schema change |
|---|---|---|---|---|
| Activity flag | `PA_TICKETS_ROLE_PERMISSION` has no `N_ACTIVE` | Role-permission table has no `N_ACTIVE` | `bool Active`; Oracle uses existing `NUMBER(1)` conversion, PostgreSQL uses `boolean` | Add non-null activity flag; PostgreSQL migration 034 defaults nulls to false; Oracle needs an equivalent `NUMBER(1)` change and check/default |
| Effective status | Absent | Absent | `string EffectiveStatus`, default `UNKNOWN`; authorization accepts only `ACTIVE` | Add `S_EFFECTIVE_STATUS VARCHAR2(20)` on Oracle / `varchar(20)` on PostgreSQL, constrained to `UNKNOWN/ACTIVE/EXPIRED/REVOKED`, with unknown default |
| Valid-from | Absent on role-permission rows | Absent on role-permission rows | `DateTimeOffset? ValidFrom` | Add nullable `TIMESTAMP WITH TIME ZONE` on Oracle / nullable `timestamptz` on PostgreSQL; never default historical dates |
| Valid-to | Absent on role-permission rows | Absent on role-permission rows | `DateTimeOffset? ValidTo` | Add nullable `TIMESTAMP WITH TIME ZONE` / `timestamptz`; enforce null or strictly later than start when both are known |
| Persisted status representation | No status column or constraint | No status column or constraint | String status, not enum-converted | Provider-specific varchar representation with identical values and checks; cross-provider migration ownership is unresolved |
| Authorization predicate | Existing baseline cannot satisfy the source query | Existing test slice cannot satisfy the source query | `Active=true`, `EffectiveStatus=ACTIVE`, known start, `start <= now`, `end > now` | Apply equivalent provider schema and query contract, then verify with provider-backed tests; no static inspection can prove translation/materialization |

Therefore the checked-in Oracle schema or an Oracle migration must add all five effective-grant elements above. The repository contains no Oracle migration runner or reviewed Oracle RBAC delta, so the Oracle DDL owner, migration ID, baseline, and rollback procedure require explicit approval. Migration 034 is PostgreSQL-specific and cannot be treated as provider parity.

### Migration readiness table

| Migration | Static result | Preconditions and preservation | Rollback limits | Remaining blocker |
|---|---|---|---|---|
| 034 permission metadata | READY FOR HUMAN REVIEW, PostgreSQL only | Verified baseline; parent tables/keys; application release using nullable start/status; preserve role-permission IDs and pairs; unknown rows denied | Remove only empty additive objects; once data is reconciled or grants are written, restore/forward-repair | PostgreSQL catalog/type verification, Oracle equivalent, 44-row reconciliation |
| 035 scopes/delegation | NEEDS CHANGES | Parent user/role/membership relations; no scope rows; organization sentinel is null organization ID; client FK deferred | New empty tables/indexes only; never delete created scopes/delegations | Owner decision on whether delegation validity remains service-enforced or requires database constraint; client semantics |
| 036 proposals | READY FOR HUMAN REVIEW | Parent role/permission/user relations; only `ROLE_PERMISSION_GRANT`; service enforces independent approver, expiry, row-version, payload hash and atomic application | Preserve proposals after use; forward repair or backup restore | Scope-proposal workflow is absent and remains blocked; provider schema verification |
| 037 built-in roles | NEEDS CHANGES / execution blocked | Exact target column types/constraints; prove `N_CREATED_BY=0` is accepted; preserve existing role IDs; inserts no permissions, memberships, users, scopes or grants | Do not delete newly referenced role rows; restore/forward repair | Target schema and governance approval for system-created rows; no stable deployed baseline |
| 038 audit actors | BLOCKED BY DECISION | Must approve actor type, nullable identity, FK behavior, writers/readers and historical preservation first | No executable rollback because the file intentionally aborts | `LEGACY_UNKNOWN`/`SYSTEM` contract unresolved |

### Historical grant reconciliation specification

The 44 role-permission rows without effective dates remain preserved by ID and role/permission pair. A read-only reconciliation export must contain only row identity, role code, permission code, current activity/status fields if available, provenance references, and a redacted evidence pointer; it must not invent dates or expose secrets.

For each row, the data owner must provide an authoritative source such as an approved role-permission change record, release manifest, or signed administrative decision that establishes the effective start, optional end, actor, reason, and intended status. A designated RBAC data owner prepares the classification; a separate security/governance approver accepts it; the application records the reconciliation audit event and provenance reference. If evidence is absent or conflicting, the row remains `UNKNOWN` and inactive, and no permission is authorized. Reconciliation must be idempotent by row ID/pair and must never rewrite the original audit history.

Client ID `872002` remains unresolved. Repository evidence establishes only five SLA policy references; it does not establish the authoritative client identity, name, ownership, or whether the number is legacy, test, or production data. The required owner decision is: identify the authoritative source and confirm that `872002` must remain stable, then approve any restrictive FK mapping after an orphan/reference manifest. No canonical client row, remapping, or FK is authorized by this review.

### Audit and organization-scope blockers

The audit model currently persists a non-null `long ActorId` and writers receive the authenticated service actor. A compatible future contract must distinguish authenticated human users, explicit system events, and unresolved imported history; define whether `ActorId` becomes nullable, how `LEGACY_UNKNOWN` is serialized, whether a nullable FK uses `ON DELETE RESTRICT`, how readers/exporters/hash verification handle null actors, and how all audit writers prevent caller-supplied spoofing. Migration 038 remains intentionally aborting until this contract and the coordinated writer/reader release are approved.

Organization-wide scope requires a separate proposal operation with: an authorized initiator, an independent active approver, self-approval rejection, expiry, idempotency/replay protection, serializable or equivalent atomic activation of the scope row, explicit `ORGANIZATION` plus null organization ID validation, audit events for proposal/decision/application, and denial of direct scope mutations. The existing `ROLE_PERMISSION_GRANT` proposal cannot authorize scope changes; `AssignRoleScopeAsync` must remain denied until this workflow exists and is tested.

### Migration execution gate

Before any migration is approved, the release/database owners must provide:

1. Exact target environment, host, port, database and user identity, plus an independently verified target fingerprint without disclosing credentials.
2. Approved migration order and immutable SHA-256 hashes for the exact files.
3. A trustworthy deployed baseline from a release record and read-only schema fingerprint, or an explicitly approved adoption procedure that records the baseline without inferring it from object existence.
4. A target-consistent backup/snapshot, recovery owner, restore test evidence, and forward-repair policy.
5. Preflight manifests and counts for users, role IDs, permission IDs, role-permission IDs/pairs, memberships, client/SLA references, tickets, and audit rows.
6. Post-migration assertions proving IDs, memberships, grants, tickets, audit values, and client references are preserved; unknown grants cannot authorize; no account receives organization scope; and built-in role provisioning added no unapproved grants.
7. Separate written approval for each operation: schema migration, built-in role provisioning, fixture creation, provider-backed tests, and cleanup. The shared `pa_tickets_test` database and the earlier incident database remain excluded.

No migration is executable on the basis of this static review alone.

## PostgreSQL schema reconciliation continuation - 2026-10-21

### Verified source facts

- `ExecutionDbContext` exposes `ReportExports`, `ReportSchedules`, and `ReportScheduleExecutions`; `ReportExportRows.cs` maps them to `PA_TICKETS_REPORT_EXPORT`, `PA_TICKETS_REPORT_SCHEDULE`, and `PA_TICKETS_REPORT_SCHEDULE_EXECUTION`.
- `database/migrations/026_phase8_6_reporting.sql` is the only checked-in reporting DDL. It creates three sequences, three tables, primary keys, the schedule-execution idempotency unique constraint, and owner/due indexes. No PostgreSQL-specific copy of migration 026 exists.
- No checked-in PostgreSQL migration creates ticket-aging evidence status/count or contributor rows. Those remain an unresolved persistence requirement, not an applied schema fact.
- `AdministrationRows.cs` requires effective-grant activity/status/date fields represented by PostgreSQL migration 034. The original schema and current provisioning slices do not contain the complete source contract.
- PostgreSQL migrations 033-038 are review artifacts. Migration 033 creates a ledger but does not infer prior application history. Migration 038 intentionally aborts and remains blocked.
- `DependencyInjection.AddInfrastructure` selects Npgsql only when `Database:Provider=PostgreSQL`; this proves source configuration support, not deployed schema compatibility.
- The provisioning scripts perform writes and are excluded from this source-only review. No live catalog state is inferred from object names or checked-in script order.

### Reconciliation discrepancies

| Area | Verified discrepancy | Consequence |
|---|---|---|
| Reporting migration location | Reporting DDL is in root `database/migrations/026_phase8_6_reporting.sql`; `database/migrations/postgresql/` has no 026 | A provider-specific migration chain and ownership decision are required before replay or adoption |
| Migration history | No runner or authoritative applied ledger is established; 033 is only a review draft | Object existence cannot establish applied versions; adoption must use a read-only fingerprint and approved baseline |
| RBAC effective grants | Source requires `N_ACTIVE`, `S_EFFECTIVE_STATUS`, `D_VALID_FROM`, and `D_VALID_TO`; baseline/provisioning slices are incomplete | 034 cannot be treated as already applied or provider-verified; unknown historical rows must remain non-authorizing |
| Scopes/delegation | Source and 035 require membership scope and delegation relations, sequences, checks, and indexes | Parent relations and single-organization sentinel must be verified before 035 |
| Proposals | Source and 036 require the implemented role-permission proposal contract; scope proposals remain unsupported | 036 must not be expanded into organization-scope authorization |
| Built-in roles | 037 inserts only missing role identities under an explicit session gate | Existing role IDs and the seed manifest must be verified; no grants or scopes may be inferred |
| Audit actors | 038 deliberately aborts; actor type/nullability/FK and writer/reader compatibility are unresolved | 038 remains blocked and cannot be included in an executable order |
| Ticket-aging evidence | No parent evidence fields, contributor entity, FK, uniqueness, or index exists in the checked-in PostgreSQL chain | Evidence persistence requires a separately approved migration after the pilot contract is approved |

### Definitive review order (proposal only)

1. Obtain written approval for a dedicated disposable target and perform a read-only catalog fingerprint.
2. Reconcile the actual baseline against root reporting migration 026, including all three sequences, table columns, keys, indexes, and existing export rows.
3. Establish or adopt migration ledger 033 only through an approved baseline procedure; do not mark files applied from object shape.
4. Apply/review 034 only after parent relation/key checks, effective-grant preservation manifests, and the fail-closed application release are approved.
5. Apply/review 035 only after user/role/membership relations and the null-organization single-org sentinel are verified; no scope rows are inserted.
6. Apply/review 036 only for the implemented role-permission proposal contract; do not represent organization-scope proposals.
7. Apply/review 037 only in a separately approved session after the five-role identity manifest is reconciled; preserve all existing IDs and insert no grants, memberships, scopes, or users.
8. Keep 038 excluded until the historical actor contract and coordinated writer/reader release are approved.
9. Design and review a later ticket-aging evidence migration (parent evidence state/count plus contributor rows) only after the export transaction and reauthorization contracts are approved.

### Required preconditions and postconditions

Every migration review requires a target fingerprint, immutable file hashes, a known baseline or approved adoption procedure, backup/recovery evidence, and preservation manifests for users, roles, permissions, role-permission pairs, memberships, client/SLA references, tickets, exports, and audit records. Postconditions must prove stable IDs and row counts, no fabricated dates or actors, no automatic scopes or grants, effective UNKNOWN rows remain denied, and all new constraints/indexes/sequences match the reviewed files.

Rollback is limited to removing empty additive objects in reverse dependency order. Once rows, grants, scopes, proposals, or role identities are written, do not delete them to simulate rollback; disable the release and use approved restore or forward repair. PostgreSQL transaction behavior must be verified on the approved target; no cross-boundary atomicity exists between external artifact storage and database persistence.

This is a source-only proposal. No migration is approved or applied, no live schema match is claimed, ticket-aging downloads remain fail-closed, and migration 038 remains blocked.
## Definitive PostgreSQL schema-build plan - 2026-10-21

### Target identity boundary

The owner reports that a dedicated PostgreSQL 18.6 database named `pa_tickets_e2e_verify` was created and verified empty through VS Code. That statement is user-provided context only; this review did not connect to PostgreSQL and does not independently verify the host, port, database user, ownership, disposability, or emptiness. The existing `pa_tickets_test` and `pa_tickets_h_verify` targets remain prohibited and untouched.

### Authoritative source baseline

The repository does not contain a single executable PostgreSQL baseline. The authoritative build input must be assembled from the checked-in PostgreSQL schema adapter scripts and reviewed additive migrations, after a read-only fingerprint confirms the target is the approved disposable database. The root Oracle-oriented schema is not itself a PostgreSQL execution baseline. Migration history must not be inferred from object shape.

### Definitive proposed order (approval required)

0. **Preflight only:** verify the exact target fingerprint, PostgreSQL version, current user, empty/approved baseline state, and immutable hashes of every selected file. Capture relation, column, constraint, index, sequence, and row-preservation manifests.
1. **Foundational PostgreSQL schema adapter:** execute only the reviewed `tests/scripts/postgresql-schema.sql` and required PostgreSQL slice scripts in their documented dependency order. This step is a separate provisioning/schema-build approval and is not represented as migration history automatically.
2. **Reporting migration 026:** execute `database/migrations/026_phase8_6_reporting.sql` after its execution/background-job prerequisites are present. Verify the three report sequences, three report tables, primary keys, schedule-execution idempotency key, owner index, and due index. No evidence tables or evidence fields are included.
3. **PostgreSQL 028:** apply `database/migrations/postgresql/028_local_password_authentication.sql` after `PA_TICKETS_USER` exists; verify additive password, rotation, phone, manager, email-verification fields, manager FK, and email index.
4. **PostgreSQL 029:** apply `database/migrations/postgresql/029_user_profile_hierarchy.sql` after the user table and manager column are present; verify only its reviewed additive hierarchy changes and existing-user preservation.
5. **Ledger 033:** apply `033_rbac_migration_ledger.sql` only after the target baseline is fingerprinted and the adoption procedure is approved. The ledger row for a migration is written only after that migration and its assertions succeed.
6. **Permission metadata 034:** apply after role/permission/role-permission parent relations and the fail-closed application release are verified. Preserve all role-permission IDs/pairs; leave unknown historical dates non-authorizing.
7. **Scopes/delegation 035:** apply after user, role, and membership relations are verified and the single-organization null-ID sentinel is approved. Insert no scopes, delegation rows, client rows, or grants.
8. **Proposals 036:** apply only for the implemented `ROLE_PERMISSION_GRANT` proposal contract. Organization-scope proposals remain excluded because the application contract is not implemented.
9. **Built-in roles 037:** run only in a separately approved session with the explicit provisioning setting, after the role identity manifest and `N_CREATED_BY=0` compatibility are approved. Preserve existing role IDs and insert no grants, memberships, scopes, or users.
10. **038 remains excluded:** `038_rbac_audit_actor_compatibility_BLOCKED.sql` intentionally aborts and must not be run.
11. **Ticket-aging evidence remains a later gate:** parent evidence fields and contributor persistence require a separately approved logical contract, EF mapping, transaction design, and migration. No evidence schema is invented by this plan.

The superseded PostgreSQL drafts `030_five_role_rbac.sql`, `031_configurable_rbac.sql`, and `032_rbac_independent_approval.sql` must not be replayed. Root migration `027_ticket_lifecycle_authority.sql` has no provider-specific PostgreSQL counterpart in the checked-in chain; its PostgreSQL applicability requires a separate reviewed adapter decision and is not silently inserted into this order.

### Required checks and rollback limits

Before each step, verify prerequisites and that the exact file hash matches the approved manifest. After each step, verify expected tables/columns/constraints/indexes/sequences, stable identity values, and no unapproved rows or grants. Before and after the build, compare users, roles, permissions, role-permission pairs, memberships, client/SLA references, tickets, exports, and audit records. Unknown effective dates remain denied; no actor, client, scope, grant, or role identity is fabricated.

Rollback is limited to dropping empty additive objects in reverse dependency order. Once rows, grants, scopes, proposals, or built-in roles exist, do not delete them to simulate rollback; use an approved backup restore or forward repair. External artifact storage and database persistence are not one atomic transaction. Migration 038, ticket-aging evidence persistence, historical export download enablement, and cleanup remain blocked.

No operation in this plan has been executed or approved by this review. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
## Final foundational dependency review - 2026-10-21

### Script graph and safety findings

| Script | Requires | Creates/changes | Empty-target and repeatability assessment |
|---|---|---|---|
| `tests/scripts/postgresql-schema.sql` | PostgreSQL; no application tables | Core tickets, work items, assignments, events, audits, audit locks/maintenance, workflow/approval/SLA execution tables, sequences, constraints and some reference rows | `IF NOT EXISTS` DDL is broadly repeatable, but it also inserts waiting reasons and audit-maintenance data and defines functions/triggers; it is not a pure schema-only stage |
| `tests/scripts/postgresql-phase5-schema.sql` | Core ticket/work-item tables from `postgresql-schema.sql` | Communications, notifications, attachments/evidence, incidents, problems, changes, escalations, relations and sequences/indexes | Mostly additive, but `SET NOT NULL`, FK and filtered-unique operations can fail on incompatible existing rows; data-bearing dependencies must be checked |
| `tests/scripts/postgresql-slice3-schema.sql` | Phase-5 communication tables | Communication templates/attempts, retry/lease columns, FKs and indexes | Additive/idempotent in parts; drops/re-adds an FK and therefore requires constraint-state verification |
| `database/migrations/025_phase74_operational_reliability.sql` | Background-job table from the core adapter | Job retry/lease/failure columns and indexes | Additive but existing null/invalid values must satisfy new constraints/defaults |
| `database/migrations/026_phase8_6_reporting.sql` | PostgreSQL transaction support; no RBAC parent relation | Report export/schedule/schedule-execution tables, three sequences, PKs, one idempotency unique constraint, owner/due indexes | `IF NOT EXISTS` is repeatable only when existing definitions match; it has no evidence persistence and no FK to requester/schedule in the checked-in file |
| `tests/scripts/postgresql-slice1-schema.sql` | Core tickets/workflow/approval tables | Department/team/user/role/permission/membership/client-membership tables and sequences, plus test reference rows and role/permission/membership inserts | It is the first RBAC-parent stage but is not schema-only: it writes fixtures/reference data, drops prior uniqueness constraints, and assumes stable test IDs |
| `tests/scripts/postgresql-slice2-schema.sql` | Slice 1 RBAC/catalogue parents plus workflow tables | Catalogue/service/request-field/value tables, ticket catalogue FKs, permission/grant inserts and idempotency indexes | Data-bearing and order-sensitive; duplicate IDs/codes or existing incompatible FKs can fail or alter semantics |
| `tests/scripts/postgresql-slice4-schema.sql` | SLA tables and RBAC permission tables | SLA policy-definition changes, indexes and permission/grant rows | Data-bearing; requires approved policy/grant preservation and existing-column compatibility |
| `tests/scripts/postgresql-slice5-schema.sql` | Phase-5 notification/communication tables | Provider/channel configuration, template/rule FKs, indexes and permission/grant rows | Additive in parts but adds constraints and data; requires orphan and duplicate checks |
| `tests/scripts/postgresql-slice6-schema.sql` | Ticket/status/priority and permission tables | Status, transition and priority tables, FKs, reference rows, permission/grant rows | Data-bearing; FK creation requires existing ticket values to be valid |
| `database/migrations/postgresql/028_local_password_authentication.sql` | `PA_TICKETS_USER` | Password hash and rotation columns | Repeatable `ADD COLUMN IF NOT EXISTS`; overlaps slice 1 columns and must have one ownership decision |
| `database/migrations/postgresql/029_user_profile_hierarchy.sql` | `PA_TICKETS_USER` and manager column | Phone, manager, email-verification columns/FK/index | Repeatable in syntax but overlaps slice 1 and 028; duplicate ownership/default semantics require review |
| `033` | RBAC parent relations and approved target fingerprint | Migration ledger table/index | Additive, but must not record historical applications without evidence |
| `034` | 033 ledger, permission and role-permission keys, fail-closed application release | Permission metadata and effective-grant fields/checks/indexes | Additive only after existing data and key shape checks; unknown rows remain denied |
| `035` | User/role/membership parents and approved single-org sentinel | Scope/delegation sequences, tables, FKs, checks/indexes | No rows inserted; client semantics and delegation constraints remain decisions |
| `036` | 033/034 parent relations and implemented proposal contract | RBAC proposal table/indexes | No rows inserted; organization-scope proposals are excluded |
| `037` | Role table/sequence and approved provisioning session | Missing built-in role identities only | Data-writing operation, separately approved; no grants/scopes/memberships/users inserted |
| `038` | None; intentionally blocked | Always raises and rolls back | Must remain excluded |

### First stage that can be considered for separate approval

The smallest meaningful foundational stage is the reviewed PostgreSQL core adapter (`postgresql-schema.sql`) followed by only the parent relations needed by reporting/RBAC. However, the repository does not provide that stage as a pure schema artifact: `postgresql-schema.sql` contains inserts/functions, and `postgresql-slice1-schema.sql` creates RBAC parents while also inserting test departments, teams, users, roles, permissions, memberships, and grants. Therefore no stage is currently approval-ready as a schema-only build.

The exact blocker is the missing separation between foundational DDL and data/provisioning content, together with overlapping ownership of user fields in slice 1 and migrations 028/029. A source-level repair plan is to split or replace the adapter into reviewed DDL-only and separately approved data/provisioning units, define one owner/order for overlapping columns, and preserve stable IDs through explicit manifests. No such repair is implemented in this review.

Reporting 026 can be reviewed after the core execution prerequisites are present, but it cannot by itself establish a complete application schema. RBAC 033 cannot run until slice-1-style parent relations exist, and 034-037 remain dependent on that reconciled baseline.

### Preservation and owner blockers

The reported empty `pa_tickets_e2e_verify` catalog is not independently verified. Before any operation, require a read-only fingerprint, exact file hashes, a baseline/adoption decision, and preflight manifests. Existing RAG tables and all prior databases remain outside scope. Client `872002`, historical effective dates, built-in-role provisioning, audit actors, organization-scope approval, and ticket-aging evidence persistence remain explicit owner gates. Migration 038 remains excluded because the actor type/nullability/FK/writer-reader contract is unresolved.

No script, migration, test, provider, or database operation was executed. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.
## DDL-only foundational adapter - 2026-10-21

A new review-draft adapter was added at `tests/scripts/postgresql-foundational-ddl.sql`. It is derived from the DDL portions of `tests/scripts/postgresql-schema.sql` and `tests/scripts/postgresql-slice1-schema.sql`; the originals remain unchanged.

The adapter contains the core ticket/work-item, assignment, event, audit, workflow, approval, SLA, idempotency, background-job, and RBAC parent-table objects, including sequences, keys, constraints, indexes, and dependency-required functions/triggers. It contains no reference-data inserts, role/permission/user/client provisioning, memberships, grants, scopes, backfills, or migration-ledger writes. Data-bearing statements were removed statically and the generated file was scanned for mutation statements.

The adapter is a review draft, not an approved migration. It starts a transaction and is intentionally subject to baseline, target-fingerprint, object-ownership, existing-data, and rollback review. User-profile columns in the adapter are sourced from slice 1; migrations 028 and 029 must not be applied again unless ownership and ordering are explicitly resolved. Reporting migration 026, the missing PostgreSQL adapter for root migration 027, and migration 025 remain separate gates. The approved RBAC order remains 033 -> 034 -> 035 -> 036 -> 037; 038 remains excluded. Ticket-aging evidence persistence is not included.

Review checklist: verify every EF-mapped object and column; sequence defaults/ownership; FK/check ordering; remaining procedural blocks; duplicate-object and constraint behavior; existing-data preservation; rollback/adoption procedure; immutable file hash; and the exact approval target. No SQL in this adapter has been executed or provider-validated.

The adapter preserves the source bodies of required audit-maintenance functions; their DELETE statements execute only when those functions are invoked. Source privilege REVOKE statements were omitted from the schema-only adapter and require a separate security/ownership decision. The adapter has not been executed or provider-validated.

## Adapter security correction - 2026-10-21

The adapter was corrected after review of the original privilege statements. All four source `SECURITY DEFINER` functions and their triggers are excluded from the first-stage adapter, including both audit-maintenance deletion functions, because the source `REVOKE ... FROM PUBLIC` policy and an approved executor/owner policy cannot be established for this stage. No privileged function is created with default PUBLIC execution. A separate security-owned stage must define ownership, fixed `search_path`, invocation authority, and explicit privilege revocation before any of these functions can be reintroduced.

Constraint guards now identify the `public` schema and intended relation (`conrelid`) in addition to the constraint name. The adapter still contains `IF NOT EXISTS`, `ALTER`, and source `DROP CONSTRAINT` operations; these are DDL operations with existing-object and data-preservation risks, not proof of idempotent or safe adoption. The adapter is therefore suitable for human review only, not execution approval.

Correction: the adapter no longer contains dependency-required functions or triggers. They are intentionally excluded with the four source `SECURITY DEFINER` definitions because the source privilege revocations and approved executor policy are not established. The earlier inventory statement that the adapter contained these functions/triggers is superseded by this security correction.

## Adapter membership-index correction - 2026-10-21

The malformed literal `\r\n` line in `tests/scripts/postgresql-foundational-ddl.sql` was replaced with normal SQL/comment lines. The duplicated membership constraint-drop text was removed; each source drop operation now appears once. Both `IX_PA_TICKETS_USER_ROLE_EFFECTIVE` and `IX_PA_TICKETS_USER_CLIENT_EFFECTIVE` are executable statements, and static index-name parity now matches the two source scripts (18 unique indexes).

The first-stage adapter still excludes the audit-integrity and SLA `SECURITY DEFINER` functions/triggers. Consequently, audit guard enforcement and waiting-SLA synchronization are unavailable from this adapter until the separate privilege/ownership policy is approved. This is an intentional security gate; no privileged function or trigger was reintroduced.

## Adapter schema-context hardening - 2026-10-21

The adapter now establishes `SET LOCAL search_path TO public, pg_catalog` inside its transaction and verifies that `public` exists before creating objects. The source objects remain unqualified to preserve their reviewed definitions, but they resolve under this controlled local context rather than an operator-provided search path. Sequence defaults were changed in the adapter to `nextval('public."..."'::regclass)` so same-named sequences cannot resolve through another schema.

Static comparison finds 37 tables, 33 unique sequences, and 18 indexes in both the combined source set and adapter. The adapter contains no `SECURITY DEFINER` functions or triggers, no literal escaped-newline sequences, and no top-level data or privilege statements. The audit-integrity and waiting-SLA behaviors supplied by the excluded functions/triggers remain unavailable pending explicit ownership, fixed `search_path`, invocation-authority, and least-privilege policy approval.

Current adapter SHA-256 (review identifier only, not execution approval): `25FC6583E3B0EFD4800298B5FA9C7BB344CFCC8887CB75CFBBC55BDC1944B79E`.

`IF NOT EXISTS`, `ALTER`, and the two membership constraint drops still do not prove idempotency, safe adoption, or rollback. Existing-object definitions and data-preservation preconditions require separate review.

## Foundational adapter preflight specification - 2026-10-21

### Immutable inputs

- Adapter after duplicate-sequence correction: `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F`
- Prior reviewed adapter hash retained for audit: `25FC6583E3B0EFD4800298B5FA9C7BB344CFCC8887CB75CFBBC55BDC1944B79E`
- `postgresql-schema.sql`: `2CCC5708C27C9DACDF26EE2B2E417832CEED9AC7DFC97D8354CF204C04AE41BF`
- `postgresql-slice1-schema.sql`: `26A6F76C0B82F85A74415733FBC11EF36A8DC93FAF60B39B7EB8CF699669129B`

The second `PA_TICKETS_APPROVAL_INSTANCE_SEQ` declaration was confirmed identical to the first and removed only from the adapter. The original source still contains its own duplicate declaration and is unchanged.

### Source-verifiable checks

- Confirm `BEGIN`, `SET LOCAL search_path TO public, pg_catalog`, and the `public`-schema existence guard precede object creation.
- Confirm all 37 tables, 33 unique sequence names/34 declarations before correction, 18 indexes, 62 `ALTER TABLE` operations, two constraint drops, and six guarded foreign keys match the hashed source inputs.
- Confirm sequence defaults use the expected public-schema sequence and no privileged function/trigger is present.
- Confirm no top-level data or privilege operation exists.

### Target-catalog checks

For every existing table, column, default, primary key, foreign key, unique/check constraint, index, and sequence, require exact schema, relation, type, nullability, default expression, predicate, referenced columns, ownership, and sequence metadata. Matching names or counts are insufficient.

The 62 alterations must be checked by their exact table/column list in the adapter. Any existing column with a different definition, any existing default mismatch, or any conflicting constraint/index aborts the proposal.

The eight expected defaults are:

- workflow `N_WORKFLOW_ID` ? `public."PA_TICKETS_WORKFLOW_SEQ"`
- workflow version `N_WORKFLOW_VERSION_ID` ? `public."PA_TICKETS_WORKFLOW_VERSION_SEQ"`
- workflow step `N_WORKFLOW_STEP_ID` ? `public."PA_TICKETS_WORKFLOW_STEP_SEQ"`
- workflow transition `N_TRANSITION_ID` ? `public."PA_TICKETS_WORKFLOW_TRANSITION_SEQ"`
- workflow instance `N_WORKFLOW_INSTANCE_ID` ? `public."PA_TICKETS_WORKFLOW_INSTANCE_SEQ"`
- workflow step instance `N_STEP_INSTANCE_ID` ? `public."PA_TICKETS_WORKFLOW_STEP_INSTANCE_SEQ"`
- approval `N_APPROVAL_ID` ? `public."PA_TICKETS_APPROVAL_SEQ"`
- SLA `N_SLA_ID` ? `public."PA_TICKETS_SLA_SEQ"`

For each, assert `pg_attrdef`, sequence type, schema, ownership, and current value compatibility.

### Target-data checks

The two membership drops require duplicate checks on `(N_USER_ID,N_ROLE_ID)` in `PA_TICKETS_USER_ROLE` and `(N_USER_ID,N_CLIENT_ID)` in `PA_TICKETS_USER_CLIENT`; existing constraint presence and relation identity must be confirmed first.

Foreign-key orphan checks are required for:

- ticket/work-item/assignment/event/approval/history/delegation/SLA rows referencing `PA_TICKETS` or `PA_TICKETS_WORK_ITEM`;
- approval-rule version/step references;
- approval-instance and approval references;
- department/team references;
- user manager, user-role, role-permission, and user-client references;
- the six guarded approval-rule-step foreign keys: role, department, team, and user columns.

Every unique or partial-unique index requires a grouped duplicate query over its exact key and predicate:

- work-item `(N_TICKET_ID,S_WORK_ITEM_NO)`;
- waiting-reason `(S_REASON_CODE,D_EFFECTIVE_FROM,D_EFFECTIVE_TO)`;
- active assignment `N_WORK_ITEM_ID WHERE D_UNASSIGNED_AT IS NULL`;
- workflow `S_WORKFLOW_CODE`;
- workflow version `(N_WORKFLOW_ID,N_VERSION_NO)` and `(N_WORKFLOW_VERSION_ID,N_WORKFLOW_ID)`;
- workflow step `(N_WORKFLOW_VERSION_ID,S_STEP_CODE)`;
- workflow transition `(N_WORKFLOW_VERSION_ID,S_TRANSITION_CODE)`;
- approval-rule step `(N_APPROVAL_RULE_VERSION_ID,N_STEP_NO,S_TARGET_KEY) WHERE N_APPROVAL_RULE_VERSION_ID IS NOT NULL`;
- workflow step instance `(N_WORKFLOW_INSTANCE_ID,N_WORKFLOW_STEP_ID)`;
- idempotency `(S_SCOPE,N_ACTOR_USER_ID,S_IDEMPOTENCY_KEY) WHERE N_ACTOR_USER_ID IS NOT NULL` and `(S_SCOPE,S_IDEMPOTENCY_KEY) WHERE N_ACTOR_USER_ID IS NULL`;
- background-job `(S_JOB_TYPE,S_DEDUPE_KEY)`;
- upper user email `upper(S_EMAIL)`;
- effective user-role `(N_USER_ID,N_ROLE_ID,D_VALID_FROM,D_VALID_TO) WHERE N_ACTIVE`;
- effective user-client `(N_USER_ID,N_CLIENT_ID,D_VALID_FROM,D_VALID_TO) WHERE N_ACTIVE`.

Inline primary/unique/check constraints also require catalog-definition assertions and data scans for duplicate keys or invalid check values. Any duplicate, orphan, invalid check value, or ambiguous existing definition aborts before installation.

### Post-install assertions

Assert the exact table/column/default/constraint/index/sequence inventory, sequence ownership and defaults, guarded FK relation identity, index predicates and uniqueness, and absence of unexpected objects. Record the migration ledger only through its approved procedure; never infer applied history from object shape.

### Abort and rollback contract

Abort on unknown target identity, unknown baseline, ambiguous migration history, schema mismatch, duplicate data, orphan references, sequence/default/ownership mismatch, conflicting constraint definition, or unresolved owner decision. PostgreSQL transactional DDL should roll back failures before `COMMIT`; after commit, restoration of dropped constraints, prior defaults, sequence state, or existing-object definitions is not guaranteed.

The four privileged audit/SLA functions and triggers remain excluded. Audit-integrity enforcement and waiting-SLA synchronization therefore remain separate security/functionality blockers requiring an approved owner, fixed `search_path`, invocation authority, and least-privilege execution policy.

## Target-specific PostgreSQL preflight proposal - 2026-10-21

Added `tests/scripts/postgresql-foundational-ddl-preflight-pa_tickets_e2e_verify.sql` as an unexecuted, target-specific read-only proposal. It asserts database `pa_tickets_e2e_verify`, PostgreSQL major version 18, a local loopback server address (reporting the actual address rather than assuming how `localhost` resolves), port 5432, role `postgres`, and schema `public`; rejects the prohibited databases; sets a read-only transaction and rolls back. The runner must require the explicit `PREFLIGHT_COMPLETE` marker and successful SQL exit status.

Implemented in the proposal: catalog inventory for public objects, columns, constraints, indexes, sequences/ownership, defaults, extensions, and migration-ledger evidence; exact duplicate checks for the adapter unique and partial-unique indexes (including both idempotency predicates, case-insensitive email, and both workflow-version keys); dynamic orphan checks for every adapter foreign key, including composite approval-rule references; exact-target and baseline assertions. Existing objects or a migration ledger cause a fail-closed baseline blocker rather than being treated as proof of applied history. The proposal is not executed or provider-validated. The preflight hash is `E20F791C463E9E61F37F77A67B292EE3D6AF437F5C474F18C1F84F1ECF5D6A7F`; the adapter remains hash `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F`.
