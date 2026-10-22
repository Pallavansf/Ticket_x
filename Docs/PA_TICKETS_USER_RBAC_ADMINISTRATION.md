# PA Tickets user and RBAC administration

## User administration

Open **Users** from the application navigation. The route and backend API enforce the existing
`USER_ADMIN` permission. The page loads users, departments and teams from the API; it supports
account creation, display-name/email/phone edits, activation and deactivation, and temporary
password assignment. The password endpoint hashes the submitted value and requires the user to
rotate it at the next sign-in. A confirmation is required before account status or password changes.

Creating a user does not assign a role or set a password. After creation, set a temporary password
and use **Role administration** to assign an authorized role. This is currently a sequence of
separate API operations, so operators should confirm each save before treating the account as ready.
The page supports department/team placement at creation. It does not provide a client catalogue,
client membership editor, reporting-manager editor, or role-scope editor because the corresponding
catalogue and approval workflow are not available as a complete UI/API flow.

## Roles and permissions

The existing **Roles and permissions** page is available to principals with
`ROLE_PERMISSION_ADMIN`. It reads the backend role and permission catalogues, shows the five
built-in identities and custom roles, and persists permission mapping changes through the
administration API. Sensitive permission grants use proposal, expiry, and independent-approver
checks. The backend authorization service evaluates effective role-permission rows and resource
scope on requests; navigation checks are only a user-interface convenience.

Custom roles cannot receive organization-wide or security-governance permissions. Built-in role
ceilings and the active-Spectator mutation veto are enforced server-side. Direct assignment of
sensitive privileges, privileged membership changes, Admin Head delegation mutations, and role
scope grants remain blocked or require an independent-approval operation that is not yet implemented
for those change types.

## Current operational limits

The PostgreSQL 031/032 drafts have not been executed. The read-only catalog review for
`pa_tickets_h_verify` found the RBAC catalogue/effective-date metadata and client/scope/delegation/
proposal structures incomplete, with no tracked migration ledger. The database repair decisions
document records the exact observations and unresolved deployment decisions. Do not use these draft
migrations for deployment until the baseline, legacy-history representation, audit actor model, and
client-ID preservation policy have been approved and the scripts repaired.

The Angular production build and database-independent suites do not establish PostgreSQL persistence
or hosted API authorization. The acceptance baseline remains 8 PASS / 3 PARTIAL / 13 BLOCKED. See
[the E2E acceptance matrix](PA_TICKETS_API_E2E_ACCEPTANCE_MATRIX.md) and
[the migration repair decisions](PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md) for evidence and
the gated database test plan.


## Capability audit (2026-10-16)

The five required role codes are defined in `RoleAuthorizationPolicy` and reserved by custom-role validation: `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, and `SPECTATOR`. The source and draft migrations define seed rows, but the observed PostgreSQL baseline lacks the required RBAC metadata and has no migration ledger; therefore default-role persistence is **UNVERIFIED**, not treated as implemented. No existing role IDs, memberships, Super Admin accounts, or organization scopes are remapped or auto-granted.

Custom-role creation, metadata editing, deactivation with active-membership and scope closure, role listing, effective-permission listing, and user-role assignment/removal are implemented behind backend permission checks and audit writes. Their persisted behavior remains **UNVERIFIED** without an approved provider test. Built-in and retained legacy identities are protected from ordinary custom-role edits/deactivation. Role assignment of privileged roles remains restricted by the existing governance rules.

The backend permission catalogue is the source of truth for modules and actions (`PA_TICKETS_PERMISSION` metadata). The API exposes role and permission catalogue endpoints, role grants, atomic permission batches, and sensitive grant proposals. Permission codes are looked up against active persisted rows; invalid or incomplete metadata is rejected. Revocation is persisted as an effective-dated inactive grant and subsequent authorization queries reload current rows. Direct sensitive grants and Super Admin permission changes remain governed by the independent-approval policy.

The Angular Roles and permissions screen consumes those APIs, groups the returned permissions by backend `moduleCode`, supports custom-role metadata, permission selection, atomic save, reload-after-save, proposal actions, validation/error feedback, and read-only review of memberships/scopes/delegations. It does not fabricate success. Navigation is guarded by `ROLE_PERMISSION_ADMIN`; backend authorization remains authoritative. The UI does not provide scope increases or privileged membership/delegation writes while their approval workflow is unavailable.

| Capability | Status | Evidence and blocker |
|---|---|---|
| Five built-in roles by default | UNVERIFIED | Stable codes and reserved identity checks exist; seed drafts are unexecuted and deployed persistence cannot be established without the repaired baseline. |
| Custom-role creation | IMPLEMENTED / persistence UNVERIFIED | `AdministrationService.CreateCustomRoleAsync`, `POST /admin/roles`, Angular role screen; provider persistence still requires approved DB testing. |
| Role editing and retirement | IMPLEMENTED / persistence UNVERIFIED | Custom-only update/deactivate, membership/scope closure, audit events; no built-in deletion path. |
| User-role assignment and revocation | PARTIAL | Backend routes and scope-aware checks exist; privileged membership and scope grants remain blocked by governance/schema readiness. |
| Module/action catalogue | IMPLEMENTED / persistence UNVERIFIED | `GET /admin/permissions` returns persisted module/action metadata; no disconnected Angular catalogue. |
| Permission mapping and persistence | PARTIAL | Atomic grants/revokes and proposal workflow exist; Super Admin grants and sensitive operations require independent approval, and deployed schema is incomplete. |
| Backend permission enforcement | IMPLEMENTED in source / provider UNVERIFIED | Central evaluator enforces active grants, declared scope, explicit organization scope, Spectator read-only rules, and revocation reloads. |
| Angular administration workflow | IMPLEMENTED in source/UI tests / hosted persistence UNVERIFIED | Route, guard, API-backed role/permission UI, reload and failure handling exist; Angular tests/build do not prove database persistence. |
| Audit and scope enforcement | PARTIAL | Audit calls and explicit organization-scope evaluator exist; historical actor/date compatibility and deployed scope tables remain unresolved. |

Required migration prerequisites remain the verified baseline, client `872002` source, unknown grant-date policy, audit actor compatibility, and independent approval support for scope grants. The next approved database action is a read-only preflight against a specifically named target, followed only after approval by a disposable provider rehearsal; no migration or fixture operation is authorized by this document.

Migration provisioning remains approval-gated: see the ordered review set in PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md. Built-in role provisioning inserts no permissions or scopes automatically, and unresolved historical actors/effective dates remain fail-closed.

Compatibility note: role-permission effective fields require an application release that treats UNKNOWN as non-authorizing before migration 034. Organization-scope approval remains blocked because no scope-proposal API exists.

Role-permission reads now expose nullable effective starts and explicit status. Unknown historical grants are visible for reconciliation but cannot authorize; new grants explicitly become ACTIVE.
