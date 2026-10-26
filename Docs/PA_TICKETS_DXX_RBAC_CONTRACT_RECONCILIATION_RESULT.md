# PA_TICKETS DXX-First RBAC Contract Reconciliation

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** source-only reconciliation of the PostgreSQL authorization-query failure  
**Provider operations:** none in this pass

## Exact discrepancy

The lifecycle provider test failed with PostgreSQL `42703` because
`DatabaseAuthorizationFactsReader` selects `AdministrationPermissionRow.ModuleCode`
(`src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationFactsReader.cs`,
permission projection) and EF maps it to `S_MODULE_CODE`
(`src/PA.Tickets.Infrastructure/Persistence/AdministrationRows.cs`,
`AdministrationPermissionRow`). The foundational PostgreSQL DDL creates
`PA_TICKETS_PERMISSION` without `S_MODULE_CODE`, `S_ACTION_CODE`,
`S_SCOPE_CLASS`, `S_SECURITY_CLASS`, or `N_READ_ONLY`
(`tests/scripts/postgresql-foundational-ddl.sql`, RBAC slice).

The first missing column is therefore a schema/application-contract mismatch,
not an authorization policy that can safely be bypassed. The next query would
also require effective-grant columns absent from the foundational table:
`D_VALID_FROM`, `D_VALID_TO`, `S_EFFECTIVE_STATUS`, `N_ACTIVE`, `D_UPDATED_AT`,
and `N_UPDATED_BY` on `PA_TICKETS_ROLE_PERMISSION`. Those fields are mapped by
`AdministrationRolePermissionRow` and filtered by the reader for active,
dated, `ACTIVE` grants.

## Authoritative source trace

1. **Application contract.** `AuthorizationPermissionFact` requires module,
   action, scope, security, and read-only metadata
   (`src/PA.Tickets.Application/Foundations/AuthorizationFactsContracts.cs`).
   `DatabaseAuthorizationService` rejects missing/blank metadata before any
   grant can authorize (`src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationService.cs`).
2. **Persistence mapping.** `AdministrationPermissionRow` maps all five
   metadata fields to `S_MODULE_CODE`, `S_ACTION_CODE`, `S_SCOPE_CLASS`,
   `S_SECURITY_CLASS`, and `N_READ_ONLY`. `AdministrationRolePermissionRow`
   maps the effective-date/status and audit fields listed above
   (`src/PA.Tickets.Infrastructure/Persistence/AdministrationRows.cs`).
3. **Reader semantics.** `DatabaseAuthorizationFactsReader` requires active
   users, active roles, active grants, `S_EFFECTIVE_STATUS = 'ACTIVE'`, a
   non-null effective start, and effective-date bounds. Missing facts produce
   no authorization; they are not treated as grants.
4. **Foundational schema.** The checked-in PostgreSQL foundational script has
   the legacy permission and role-permission shapes only; it does not declare
   the configurable metadata/effective-grant contract.
5. **Migration 031.** `database/migrations/postgresql/031_configurable_rbac.sql`
   adds the metadata and effective-grant columns, populates a draft 36-code
   catalogue, grants permissions, and contains a `$scope_backfill$` block that
   derives scope rows from memberships. Its header and owner packet identify
   it as a draft whose role/grant/scope behavior is not approved.
6. **Migration 034.** `database/migrations/postgresql/034_rbac_permission_metadata.sql`
   is the closer fail-closed shape: it adds the metadata and provenance-aware
   effective-grant fields, defaults `S_EFFECTIVE_STATUS` to `UNKNOWN`, marks
   unknown grants inactive, and performs no data backfill. It requires the
   migration-ledger/baseline prerequisite and remains a review draft.
7. **Migration 030.** `database/migrations/postgresql/030_five_role_rbac.sql`
   is explicitly superseded and rejects execution unless exactly its reviewed
   fixed 34-code active catalogue exists. That guard was not bypassed.

The owner decision pack records the reviewed configurable draft as 36 codes,
while preserving D04 (canonical codes), D05 (historical grant treatment), D08
(role/grant matrix), D01 (baseline), D02/D03 (operator and sequence safety),
and D09 (unknown migration history) as formally `PENDING`. The pack also
records that the approved target currently lacks `S_MODULE_CODE`.

## Source-only correction assessment

No safe application-only correction exists. Removing `ModuleCode` and related
fields from the projection, inventing defaults, or catching PostgreSQL
undefined-column errors would either weaken the explicit metadata contract or
turn unknown authorization facts into an implicit allow/ambiguous result.
The existing fail-closed behavior is the correct response to an unprepared
schema. No production code or tests were changed, and no focused test was run
in this pass.

## Unexecuted schema compatibility proposal

The following is a **proposal for owner review, not an executable migration**.
It describes the minimum structural delta needed before the current reader can
translate against the legacy RBAC tables; it deliberately does not insert
permissions, roles, grants, scopes, delegation rows, or ledger history:

```sql
-- Proposal only; do not execute without D01/D02/D03/D04/D05/D08/D09 approval.
ALTER TABLE "PA_TICKETS_PERMISSION"
  ADD COLUMN IF NOT EXISTS "S_MODULE_CODE" varchar(80),
  ADD COLUMN IF NOT EXISTS "S_ACTION_CODE" varchar(50),
  ADD COLUMN IF NOT EXISTS "S_SCOPE_CLASS" varchar(30),
  ADD COLUMN IF NOT EXISTS "S_SECURITY_CLASS" varchar(40),
  ADD COLUMN IF NOT EXISTS "N_READ_ONLY" boolean;

ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
  ADD COLUMN IF NOT EXISTS "D_VALID_FROM" timestamptz,
  ADD COLUMN IF NOT EXISTS "D_VALID_TO" timestamptz,
  ADD COLUMN IF NOT EXISTS "S_EFFECTIVE_STATUS" varchar(20) NOT NULL DEFAULT 'UNKNOWN',
  ADD COLUMN IF NOT EXISTS "S_PROVENANCE_REF" varchar(300),
  ADD COLUMN IF NOT EXISTS "N_ACTIVE" boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS "D_UPDATED_AT" timestamptz,
  ADD COLUMN IF NOT EXISTS "N_UPDATED_BY" bigint;
```

This shape is derived from migration 034 and intentionally leaves permission
metadata and grant dates unknown. It cannot by itself make API authorization
work: an owner-approved catalogue and grant matrix must populate valid metadata
and only evidence-backed active grants may be marked `ACTIVE`. No `DEFAULT`
module/action/scope/security value is safe to invent. The existing application
also requires `ValidFrom` and `EffectiveStatus = 'ACTIVE'` for a grant, so
unknown historical rows remain denying.

### Expected state change

- **Before:** legacy permission and role-permission columns only; reader fails
  at `S_MODULE_CODE` and cannot establish authorization facts.
- **After structural step:** all mapped columns exist; unknown metadata/grants
  remain non-authorizing until separately reconciled.
- **After approved data step:** only D04/D05/D08-approved permission rows,
  roles, effective grants, provenance, and scopes may be written. No historical
  migration is inferred from object shape.

### Compatibility, rollback, and provenance requirements

- The structural step is additive but must be preceded by D01 baseline
  equivalence/fingerprint review, D02 operator approval, D03 sequence method,
  and D09 explicit unknown-history treatment.
- D04/D05/D08 approval is required before any catalogue, role, grant, or
  effective-date data is inserted or backfilled. D06/D07 remain prerequisites
  for scope/delegation objects; the migration 031 membership-derived
  `$scope_backfill$` behavior conflicts with the owner-proposed explicit model.
- A transaction/rollback plan must identify whether each provider operation
  can be reversed safely and must preserve the pre-change baseline artifact.
  No rollback claim is made from this source review.
- Migration 033 must not receive fabricated applied rows. If adopted, its
  ledger entries require exact artifact SHA-256, target fingerprint, approval,
  and release-baseline evidence.
- Migration 038 remains intentionally blocked pending the audit-actor contract.
  Migration 026 remains a separate authorization chain.

## Next DXX decision

The immediate owner decision is **D04/D05**, jointly with the prerequisite
baseline/history decisions **D01 and D09**: approve the configurable
permission metadata contract, the 36-code catalogue (or an explicitly
approved alternative), and fail-closed treatment of unknown historical grants.
No provider rerun or schema operation should occur until those decisions,
least-privilege operator D02, sequence method D03, and the separately required
grant/role decision D08 have attributable approval evidence.

## Preservation

Only this report was created. No application, migration, environment, or
configuration file was modified. No PostgreSQL/Oracle connection, SQL
operation, migration, DDL/DML, provisioning, seed, cleanup, privilege change,
or migration-026 operation occurred. The branch and all pre-existing dirty and
ignored worktree content were preserved.

