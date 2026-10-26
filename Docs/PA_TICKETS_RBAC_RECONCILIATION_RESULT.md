# PA_TICKETS RBAC Contract Reconciliation and Migration Readiness

**Repository:** `G:\WSS\aw\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** source review and owner-decision preparation only  
**Status:** RBAC migration remains blocked pending owner decisions

## Result

The application and migration sources agree that persisted authorization is
metadata-driven. `DatabaseAuthorizationFactsReader` requires an active
permission row with code, module, action, scope, security, and read-only
metadata, plus date-valid memberships, active grants with
`S_EFFECTIVE_STATUS = 'ACTIVE'`, and valid scopes. The reviewed target evidence
still reports that `S_MODULE_CODE` is absent from the approved permission
catalogue. No permission rows, grants, migration-ledger rows, privileges, or
schema objects were changed by this work.

The additive columns in migration 031 are initially nullable, but
`DatabaseAuthorizationService` rejects missing or blank module, action, scope,
or security metadata. A column-only change would therefore leave the blocker in
place and is not a safe resolution.

The reviewed configurable catalogue in
`database/migrations/postgresql/031_configurable_rbac.sql` contains 36
permission definitions. Its grant and scope backfill blocks are draft
proposals, not approvals. The superseded fixed 34-code catalogue must not be
used as a substitute. `AUDIT_ACTIVITY` is a report identifier in
`AuditorReportingService`; report authorization uses `AUDITOR_VIEW`, so the
two concepts must remain separate.

## Source-to-schema matrix

| Requirement | Source evidence | Required persisted facts | Readiness |
|---|---|---|---|
| Active actor | `AdministrationRows.cs`; `DatabaseAuthorizationFactsReader.cs` | Active `PA_TICKETS_USER`, department/team context | Source contract established; historical lineage unresolved |
| Permission metadata | `AdministrationPermissionRow`; `DatabaseAuthorizationFactsReader.cs` | `S_PERMISSION_CODE`, `S_MODULE_CODE`, `S_ACTION_CODE`, `S_SCOPE_CLASS`, `S_SECURITY_CLASS`, `N_READ_ONLY`, `N_ACTIVE` | Blocked by missing module metadata and catalogue approval |
| Membership | `AdministrationUserRoleRow`; reader join | Active role membership with valid interval | D05/D08 approval required |
| Effective grant | `AdministrationRolePermissionRow`; reader filters | Active grant, `ACTIVE` status, valid-from/to interval | Unknown history must deny; backfill policy pending D05 |
| Scope | `AdministrationUserRoleScopeRow`; `ScopeAuthorizationPolicy.cs` | Explicit effective-dated scope rows | Client relationship and derivation pending D06/D07 |
| Delegation | `AdminHeadDelegationRow`; `AdminHeadDelegationPolicy.cs` | Effective Admin Head-to-Admin delegation | D07 pending |
| Audit actor | `AuditRow`; `038_rbac_audit_actor_compatibility_BLOCKED.sql` | Actor type/FK/nullability and hash-chain rules | D10 pending; migration 038 intentionally aborts |
| Migration history | `033_rbac_migration_ledger.sql` | Artifact hash, baseline, target fingerprint, status | D09 pending; history remains unknown |

## Owner decisions and blockers

The existing `Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md` remains
the decision record. All decisions remain `PENDING` because no verifiable owner
approval exists in repository evidence.

- **D01 baseline:** approve or reject the exact foundational definition and
  hash comparison before any schema-change request.
- **D02 operator:** approve a dedicated non-superuser or a narrowly scoped,
  time-limited exception for the current superuser.
- **D03 sequences:** approve reconciliation of existing keys, defaults,
  ownership, and future allocation without reset/advance assumptions.
- **D04 catalogue:** approve canonical uppercase codes, aliases, duplicate
  handling, and the source of truth for the 36-code draft.
- **D05 historical grants:** approve effective dates, revocation, provenance,
  and fail-closed treatment of unknown history.
- **D06/D07 scope:** approve client identity/FK behavior and whether scope and
  delegation rows are derived or explicitly authored.
- **D08 roles/grants:** approve the five role identities and the proposed
  grant matrix separately from role-table creation.
- **D09 ledger:** approve an explicit representation of unknown history without
  fabricating applied migrations; 028/029 and 026 must not be inferred.
- **D10 audit actor:** approve actor types, FK/nullability, compatibility, and
  hash-chain preservation before any audit DDL.
- **D11 migration 026:** retain its independent authorization, exact artifact
  hash, target identity, locking/recovery, and execution gates.

## Replacement migration proposal

No executable replacement migration was created and no existing migration was
modified. The source-aligned order for a future, separately authorized change
is:

1. Read-only target, baseline-hash, schema-definition, history, and operator
   preflight; abort on any ambiguity or prohibited target.
2. Add only approved permission/grant metadata and indexes, preserving IDs and
   refusing invented historical values.
3. Reconcile the owner-approved catalogue, preserving matching IDs and rejecting
   unknown, duplicate, case-variant, or conflicting definitions.
4. Apply only approved role/grant semantics; `UNKNOWN` effective history stays
   non-authorizing.
5. Add scope/delegation relations only after D06/D07; no unapproved backfill.
6. Represent migration history only after D09; never mark 026, 028, or 029
   applied from object shape.
7. Consider 036 and 037 separately under their proposal/session gates.
8. Keep 038 blocked until D10 resolves the audit actor contract.
9. Perform read-back and fail-closed validation, with recovery/reconciliation
   for uncertain outcomes.

This is a readiness proposal, not execution authority. Migration 026 remains a
separate workstream.

## Static checks and changes

Inspected without provider access:

- `DatabaseAuthorizationFactsReader.cs`, `AdministrationRows.cs`,
  `TicketsDbContext.cs`, `RoleAuthorizationPolicy.cs`,
  `ScopeAuthorizationPolicy.cs`, and `AdminHeadDelegationPolicy.cs`.
- PostgreSQL migration drafts 031, 033, 034, 035, 036, 037, and the blocked
  038 script.
- The master roadmap, RBAC gate resolution, persistence execution report,
  ticket-journey report, and existing owner packet.
- `git branch --show-current` and `git status --short --ignored` before and
  after editing.

Changed files:

- `Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md` — corrected the
  reporting-code wording and added the source matrix, exact catalogue summary,
  role/grant evidence, migration-readiness sequence, and blockers.
- `Docs/PA_TICKETS_RBAC_RECONCILIATION_RESULT.md` — this report.

No application code, tests, migrations, schema scripts, environment files, or
roadmap statuses were changed. No source-level test was justified because the
identified issue is an owner-gated schema/catalogue dependency rather than a
safe, provider-independent implementation defect.

## Safety and next action

No PostgreSQL or Oracle connection, SQL, migration, DDL, DML, provisioning,
seeding, cleanup, sequence operation, privilege change, or provider-backed test
was performed. The prohibited databases and role were not accessed. The next
action is to obtain explicit owner decisions D01-D10 in the existing packet;
only after those decisions may a separately reviewed schema-change request be
drafted. API and Angular E2E remain blocked, and no RBAC resolution is claimed.

**Worktree:** branch preserved; all pre-existing tracked, untracked, ignored,
and local configuration changes were preserved. No reset, clean, stash, branch
switch, commit, or push was performed.

**RBAC readiness:** `BLOCKED — OWNER DECISIONS AND PERMISSION METADATA REMAIN UNRESOLVED`
