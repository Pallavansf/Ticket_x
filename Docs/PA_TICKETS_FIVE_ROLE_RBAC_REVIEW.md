# PA TICKETS five-role RBAC review — superseded

> **Superseded; do not approve or execute the fixed-allowlist migration proposal in this
> document.** The configurable enterprise RBAC design and migration plan are now in
> [`PA_TICKETS_CONFIGURABLE_RBAC_DESIGN.md`](./PA_TICKETS_CONFIGURABLE_RBAC_DESIGN.md). The
> `030_five_role_rbac.sql` artifacts are not approved for execution and must be revised to
> implement that design. No database migration or account write has been performed.

## Prior review gate (retained for audit context)

The implementation and provider-specific migration artifacts are prepared for review only. No migration was run and no privileged account data was written. The live disposable database is `pa_tickets_h_verify`, accessed as `pa_tickets_test_user`.

Existing role identities are preserved: ID 1 `OPERATIONS`, ID 2 `ADMINISTRATOR`, and ID 3 `AUDITOR`. The five new role IDs are allocated from the existing role sequence, skipping any occupied numeric ID. The migration does not repurpose IDs 1–5.

## Five-Role RBAC + Module Controls - IMPLEMENTATION MILESTONE COMPLETE

The source implementation milestone is recorded complete for the five built-in roles `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, and `SPECTATOR`. It covers the RBAC foundation, custom-role management, role-membership administration, module/action permission controls, centralized authorization, and the Angular administration interface across `APPROVALS`, `AUDIT`, `CATALOGUE`, `CLIENTS`, `COMMUNICATIONS`, `CONFIGURATION`, `NOTIFICATIONS`, `SECURITY`, `SLA`, `TICKETS`, `USERS`, and `WORKFLOWS`.

This record does not approve or revive the superseded fixed-allowlist migration. PostgreSQL/Oracle persistence reconciliation, migration execution, provider testing, and full API/Angular end-to-end acceptance remain separate release gates.

## Reviewed permission matrix

| Role | Permission codes | Resource scope and restrictions |
|---|---|---|
| `USER` | `TICKET_READ`, `TICKET_UPDATE` | Existing user/client/department/team and ticket assignment checks apply. Ticket search must have explicit scope; reads and updates are not organization-wide. |
| `ADMIN` | `TICKET_READ`, `TICKET_UPDATE`, `USER_ADMIN`, `ROLE_ADMIN` | Ticket activity is department-scoped. User and role operations require an explicit target/department resource and are limited to the Admin's department. May grant/remove only `USER` and `SPECTATOR`; cannot manage Admin, Admin Head, Super Admin, legacy Administrator, clients, global configuration, or department lifecycle. |
| `SUPER_ADMIN` | The current reviewed active catalog: `APPROVAL_ADMIN`, `APPROVAL_PUBLISH`, `APPROVAL_READ`, `AUDIT_EXPORT`, `AUDITOR_VIEW`, `CATALOGUE_PUBLISH`, `CATEGORY_ADMIN`, `CATEGORY_READ`, `CLIENT_ADMIN`, `COMMUNICATION_ADMIN`, `COMMUNICATION_PUBLISH`, `COMMUNICATION_READ`, `COMMUNICATION_RETIRE`, `CONFIG_ADMIN`, `NOTIFICATION_ADMIN`, `NOTIFICATION_PUBLISH`, `NOTIFICATION_READ`, `NOTIFICATION_RETIRE`, `REQUEST_TYPE_ADMIN`, `REQUEST_TYPE_READ`, `ROLE_ADMIN`, `SERVICE_ADMIN`, `SERVICE_READ`, `SLA_ADMIN`, `SLA_PUBLISH`, `SLA_READ`, `SLA_RETIRE`, `TICKET_ADMIN`, `TICKET_READ`, `TICKET_UPDATE`, `USER_ADMIN`, `WORKFLOW_ADMIN`, `WORKFLOW_PUBLISH`, `WORKFLOW_READ` | Organization-wide, subject to each service's explicit permission and workflow/separation-of-duties checks. The code allowlist intentionally fails closed for permission codes added later until this matrix and migration are reviewed. Another Super Admin's privileged root profile/memberships remain protected through ordinary administration; the dedicated transition process is still required for root-level changes. |
| `ADMIN_HEAD` | `TICKET_READ`, `TICKET_UPDATE`, `USER_ADMIN`, `ROLE_ADMIN` | Same department boundary as Admin. Management of a user holding `ADMIN` additionally requires a current, explicit, effective-dated Admin Head-to-Admin delegation. Reporting-manager links never confer delegation. May remove a delegated Admin membership, but may not create an Admin or grant `ADMIN_HEAD`/`SUPER_ADMIN`. |
| `SPECTATOR` | `APPROVAL_READ`, `AUDITOR_VIEW`, `CATEGORY_READ`, `COMMUNICATION_READ`, `NOTIFICATION_READ`, `REQUEST_TYPE_READ`, `SERVICE_READ`, `SLA_READ`, `TICKET_READ`, `WORKFLOW_READ` | Existing record-level scopes apply. Every authenticated non-GET/HEAD/OPTIONS API request is denied if any effective active Spectator membership exists, even when another role grants a write permission. No non-GET read operation is designated safe in this release. |

`OPERATIONS` and `AUDITOR` retain their existing database-backed mappings; the migration does not edit their grants. Effective grants continue to require active user, role, permission, and effective membership records. Authorization is resolved from the database for each backend permission decision; JWT role/permission claims are presentation hints and may be stale. Spectator membership has deny precedence over all mutation permissions.

### Permission-to-operation map

The permission code is checked in the application service/repository that performs the operation; the route is not authorized by role name alone.

| Permission code(s) | Protected operation families | Scope |
|---|---|---|
| `TICKET_READ`, `TICKET_UPDATE`, `TICKET_ADMIN` | Ticket search/read/create/update, assignment, lifecycle transitions, work-item actions, attachments/evidence, and ticket-linked incident/problem/change flows | Ticket department/team/client/assignee constraints and workflow-specific authorization apply. `USER` and legacy `ADMINISTRATOR` retain only ordinary read/update grants. |
| `USER_ADMIN` | User list/profile/password/active state, department/team administration | Admin/Head require the user's department; Admin Head must have a live delegation for a privileged `ADMIN` target. Department lifecycle is Super Admin only (`CONFIG_ADMIN`). |
| `ROLE_ADMIN` | Role/permission catalogue reads, role membership grants/revocations, Admin Head delegation lifecycle | Privileged grants are Super Admin only. Admin and Head can only make department-scoped `USER`/`SPECTATOR` changes; Head can manage an assigned Admin. |
| `CLIENT_ADMIN` | User-client access assignments and removals | Super Admin only in this matrix; user-client effective dates remain authoritative. |
| `SERVICE_READ`, `CATEGORY_READ`, `REQUEST_TYPE_READ` | Service/category/request-type catalogues and ticket creation reference data | Existing active/effective data and ticket scope rules apply. |
| `SERVICE_ADMIN`, `CATEGORY_ADMIN`, `REQUEST_TYPE_ADMIN`, `CATALOGUE_PUBLISH` | Catalogue creation, update, retire, and publication | Super Admin only unless a later reviewed scope model assigns department ownership. |
| `WORKFLOW_READ`, `WORKFLOW_ADMIN`, `WORKFLOW_PUBLISH` | Workflow definitions, validation, publication, and version lifecycle | Super Admin for writes; existing workflow version, assignment, and separation-of-duties checks remain. |
| `APPROVAL_READ`, `APPROVAL_ADMIN`, `APPROVAL_PUBLISH` | Approval rules, publication, approval decision/history paths | Super Admin for configuration; designated approver and separation-of-duties checks remain required. |
| `SLA_READ`, `SLA_ADMIN`, `SLA_PUBLISH`, `SLA_RETIRE` | SLA policy/calendar reads and lifecycle changes | Super Admin for writes; existing effective-dated SLA policy checks remain. |
| `COMMUNICATION_READ`, `COMMUNICATION_ADMIN`, `COMMUNICATION_PUBLISH`, `COMMUNICATION_RETIRE` | Communication configuration, templates, channel/provider controls | Super Admin for writes; provider activation and effective-window checks remain. |
| `NOTIFICATION_READ`, `NOTIFICATION_ADMIN`, `NOTIFICATION_PUBLISH`, `NOTIFICATION_RETIRE` | Notification rules and delivery configuration | Super Admin for writes; existing publication/delivery controls remain. |
| `CONFIG_ADMIN` | Global operational and department lifecycle configuration | Super Admin only. |
| `AUDITOR_VIEW`, `AUDIT_EXPORT` | Audit/report views and exports | Auditor retains existing read/export behavior; Super Admin receives the reviewed catalog. Spectator does not receive export. |

This is the approved role-policy baseline, not evidence that every endpoint has passed an integration test. The existing controller/service inventory still needs route-by-route and browser acceptance after the migration review.

## Admin Head delegation model

`PA_TICKETS_ADMIN_HEAD_DELEGATION` represents an explicit `(Admin Head user, Admin user, valid-from, valid-to, active)` delegation, with foreign keys to the existing user table. Creation requires active user accounts and active `ADMIN_HEAD` and `ADMIN` memberships that cover the full requested delegation period. Effective authorization rechecks the delegation and the Admin's current effective role. The reporting-manager field is not consulted.

The row is never physically deleted by the application. Assignment/removal requires a recorded reason and appends administration audit-chain events; removal closes an already-started effective interval, or marks a future delegation inactive without creating an invalid date range. The endpoints are under `/api/v1/admin/admin-head-delegations` and require `ROLE_ADMIN`, which only `SUPER_ADMIN` receives without target scope. Concurrent migration/application deployment must be coordinated because this code expects the new table.

## Legacy Administrator transition proposal (not executed)

Read-only inspection of the approved disposable database found one effective active `ADMINISTRATOR` membership: opaque PA TICKETS user ID **9001**. The row is active and has department and team references. The database contains 34 mapped permissions on the legacy role. The account's human owner and operational purpose are not verifiable from redacted database evidence and must be confirmed by the application owner before any transition. No login name, email, password/hash, or signing configuration is recorded here.

Proposed result, contingent on that identity confirmation: replace only this membership's effective authority with `SUPER_ADMIN`, whose effective rights are exactly the current reviewed 34-permission allowlist above. Do not infer approval from the presence of the membership or the bootstrap marker. `ADMINISTRATOR` remains in the database for historical compatibility, but runtime policy now recognizes only `TICKET_READ` and `TICKET_UPDATE` from it and applies normal department/team/client scope; its 32 other mapped grants no longer authorize requests. There is no special global administrator bypass.

Auditable transition procedure for separate owner approval:

1. Confirm user ID 9001 with the account owner and an independent reviewer using an approved identity source.
2. In one reviewed transaction, close/deactivate the existing effective `ADMINISTRATOR` membership at the approved UTC instant and insert a new effective `SUPER_ADMIN` membership beginning at that instant. Do not reuse the old membership row or rewrite audit history.
3. Append hash-chained audit events recording the old/new role IDs, target user ID, actor/operator, reason/change reference, and timestamp; never include credential material.
4. Verify effective grants and role dates using a read-only query and exercise a designated administrative operation with the account owner present.

Rollback is compensating, not destructive: close the new Super Admin membership, insert a fresh, dated `ADMINISTRATOR` membership under a new approved change, and append audit events. The code will still restrict that legacy role to scoped ticket read/update. This does not erase the temporary Super Admin authority already exercised, so audit review and incident assessment remain necessary.

## Migration and rollback notes

- `database/migrations/030_five_role_rbac.sql` is the Oracle artifact; `database/migrations/postgresql/030_five_role_rbac.sql` is its PostgreSQL counterpart.
- Apply after migration 029 in the matching provider chain. No repository migration runner was found; deployment must use the existing controlled/manual database-change procedure.
- New role and role-permission primary keys are allocated from existing sequences and collision-checked. No numeric role ID is assumed. The migration fails if the expected 34 active permission codes are not present.
- Repeated execution is designed to add only missing role codes, mappings, sequence/table/index objects. Existing memberships, role IDs/codes, user IDs, ticket rows, and historical audits are not updated.
- PostgreSQL DDL is transaction-wrapped. Oracle DDL commits implicitly; partial execution must be inspected and safely rerun. Both scripts are additive, but the new role rows and delegations must not be dropped during rollback if assignments/audits reference them.
- Preferred rollback is a forward corrective migration that closes memberships/delegations and reverts application policy after a reviewed, audited plan. Dropping the delegation table or role definitions is not a safe rollback. Reverting to old application code would restore the `ADMINISTRATOR` global bypass and is not an acceptable standalone rollback.
- No migration, delegation, membership, or privileged account write has been executed for this change.

## Verification status and required review

Implemented artifacts currently include backend permission allowlists, scoped Admin/Head checks, explicit delegation storage and endpoints, Spectator mutation denial, Super Admin bootstrap semantics, and fail-closed Angular route/permission checks. The API and bootstrap utility builds passed with no warnings or errors; the focused role-policy suite passed 9/9. Angular auth/guard tests previously passed 7/7. Full role integration tests, migration repeatability/data-preservation tests, live Angular-to-API-to-PostgreSQL browser flows, and authenticated route-by-route acceptance remain unverified.

Before any migration or privileged account write, review and approve this document, confirm that opaque user ID 9001 is the intended owner account, approve the role matrix and transition procedure, and authorize the exact database target. The live target must be independently rechecked as `pa_tickets_h_verify` with database role `pa_tickets_test_user` immediately before a later approved test-write batch.
