# PA TICKETS configurable RBAC design

## Status and scope

**Approved design baseline; the Five-Role RBAC + Module Controls implementation milestone is complete in source and Angular administration. Provider-migration artifacts remain unreviewed, and no database migration or account/membership write has been performed or authorized.**

This document supersedes the fixed-allowlist proposal in
[`PA_TICKETS_FIVE_ROLE_RBAC_REVIEW.md`](./PA_TICKETS_FIVE_ROLE_RBAC_REVIEW.md). The current
`030_five_role_rbac.sql` files are first-pass artifacts, not approved deployment scripts: they
seed a fixed permission set, do not provide an application permission editor, do not describe a
complete module/action catalogue, and do not retire or map the legacy `OPERATIONS` and `AUDITOR`
memberships. They must be revised and reviewed against this design before any execution.

The database inventory previously recorded existing role identities as ID 1 `OPERATIONS`, ID 2
`ADMINISTRATOR`, and ID 3 `AUDITOR`. New role IDs must be allocated from the existing sequence;
IDs 1–5 are not reserved. The legacy `ADMINISTRATOR` account transition remains a separate,
owner-confirmed change. User ID 9001 is not approved for promotion or demotion.

## Implementation milestone status

The source implementation milestone is complete for the five built-in roles `USER`, `ADMIN`,
`SUPER_ADMIN`, `ADMIN_HEAD`, and `SPECTATOR`, including custom-role management, role-membership
administration, module/action permission controls, centralized authorization, and the Angular
administration interface. This is an implementation record, not a deployment or provider-acceptance
claim. The current worktree remains subject to migration, provider, and end-to-end release gates:

- Backend policy now reads effective, database-backed permission mappings, applies role security
  ceilings and Spectator deny-overrides, and requires explicit effective membership scopes for
  scoped standard-role permissions. A later continuation adds two-person proposals for the
  constrained permission-grant subset documented below; new `SUPER_ADMIN` permission grants
  remain blocked.
- Administration API/service and an Angular role/permission page support custom-role lifecycle,
  ordinary mapping edits, membership listing/assignment, scope history, and explicit Admin Head
  delegation history. Eligible high-impact permission grants use the independent proposal flow;
  unsupported delegation mutations, scope grants, and privileged membership grants remain
  fail-closed. Removal operations that only narrow access remain available and are audited.
- PostgreSQL and Oracle migration 031 drafts add permission metadata, effective mapping state,
  scope/delegation storage, the five built-ins, seed grants, and legacy scope backfill. Neither
  script has been executed or validated against a live provider. The Oracle draft has implicit-DDL
  commit/rollback limitations.
- At the original preparation checkpoint, safe verification included: focused RBAC policy tests
  (12), Angular role-administration component tests (6), the full Angular suite (40 tests across 12
  files), and a successful Angular production build. The ordinary .NET test build initially hit
  file locks because the API process was already using the API output files; all referenced
  projects were then rebuilt to an isolated temporary output directory and the focused test passed.
  No PostgreSQL integration test, migration execution, real-browser journey, or full API
  acceptance batch was run. Database identity was not revalidated for that historical work.
- The admin page does not deliver separate SLA, workflow, approval, notification, or business-policy
  configuration screens. Those domains remain separate and unimplemented at the UI layer.
- The role editor originally applied permission changes through separate API requests. A later
  continuation added an atomic batch endpoint with an expected effective-grant snapshot and a
  serializable transaction; see the continuation review below for its validation status.

### Independent-approval implementation continuation — 2026-10-14

This is a safe, incomplete implementation slice, not approval to deploy:

- A persistent proposal workflow now covers permission grants to protected built-in roles other
  than `SUPER_ADMIN`, plus sensitive grants to eligible custom roles where the immutable role
  ceiling permits the permission. Only a distinct, currently active `SUPER_ADMIN` membership with
  current `ROLE_PERMISSION_ADMIN` authorization may approve or reject. The proposal initiator
  cannot decide their own proposal. A second independently bootstrapped Super Admin account is a
  runtime prerequisite; no account was inspected, created, or changed.
- Lifecycle is `PENDING` → `APPROVED` → `APPLIED` in one transaction, or terminal
  `REJECTED`, `EXPIRED`, or initiator-only `CANCELLED`. `APPROVED` is transaction-internal and
  cannot be observed separately from application. A stale target/policy snapshot becomes a
  rejected proposal with `SNAPSHOT_MISMATCH`. Proposals expire 24 hours after creation; listing
  expires visible pending rows in bounded batches of 50.
- Proposals contain a server-generated opaque UUID, exact permission/role IDs and metadata,
  expected effective permission IDs, initiator, creation/expiry times, reason, idempotency key,
  correlation ID, status, decision, and a server-computed SHA-256 digest over canonical JSON for
  the immutable operation fields. Approval recomputes the payload server-side, compares immutable
  fields—including the target role's system-role flag—with the proposal row and current
  role/permission metadata, then verifies the digest. The audit hash chain records the payload
  digest.
  Proposal creation, decisions, expiry, snapshot mismatch, mapping application, and final status
  are appended through the existing audit service in the same transaction as their corresponding
  state changes in the service's transaction boundary. Source control flow does not return a grant
  when an audit append fails, but provider atomicity and ambiguous commit behavior remain unverified.
- Serializable transactions, proposal-row optimistic concurrency versioning, current-state
  revalidation, a unique initiator/idempotency-key constraint, and idempotent repeated create and
  same-approver apply requests are the implemented controls intended to prevent duplicate grants.
  Their race, uniqueness, and rollback guarantees are not established until exercised against each
  provider. Approval rechecks active user/membership, immutable role ceilings, permission metadata,
  the payload digest, and the full expected mapping snapshot. Application exceptions before a
  confirmed commit are not returned as success; whether a failed/ambiguous transaction leaves the
  durable proposal `PENDING` or `APPLIED` requires provider failure-injection and retry validation.
  There is no persisted `FAILED` state. The idempotent-create unique-key recovery path disposes its
  rolled-back EF transaction before reloading a possible winner; this has compile/unit coverage but
  still needs concurrent provider verification.
- API routes add proposal creation, visible-list/detail reads, approve, reject, and cancel under
  the versioned administration controller. Proposers see their proposals; only currently eligible
  approvers see the broader review queue. Detail access for other actors is concealed as not found.
  The initiator and approver identities are server-derived; clients cannot choose an approver.
- The Angular role editor can propose eligible grants, displays the pending queue and current
  proposal details, hides approve/reject actions from the initiator, requires rejection/cancellation
  reasons, prevents duplicate clicks, retains create idempotency keys in same-tab session storage
  across reloads until a successful response, refreshes after decisions, disables actions on locally
  expired pending proposals, and preserves decision errors across a queue refresh.
- Proposal list/detail reads now use a serializable transaction spanning authority and data reads,
  reducing stale-privilege exposure between an authority check and returning another user's
  proposal. This still requires provider-backed isolation testing.
- Explicit operation coverage:

  | Operation | Current behavior |
  |---|---|
  | Sensitive permission grants to eligible roles | Proposal workflow, independent active Super Admin, snapshot validation, atomic audited apply |
  | Grants to protected built-in roles except `SUPER_ADMIN` | Proposal workflow even for lower-impact permissions |
  | New `SUPER_ADMIN` permissions or changes to its ceiling | Blocked; no independent approval authority outside the role being changed |
  | `ORGANIZATION_ADMIN` / `SECURITY_GOVERNANCE` grants | Blocked by immutable role ceilings; no proposal supported |
  | Permission revocation, including `SUPER_ADMIN` mapping revocation | Direct scoped revocation remains available to reduce access and is audited |
  | `ADMIN` / `ADMIN_HEAD` / `SUPER_ADMIN` membership grants | Blocked; no membership proposal/application handler |
  | Role-scope increases | Blocked; no scope proposal/application handler |
  | Admin Head delegation create/expansion/removal | Blocked; no delegation proposal/application handler |
  | Approval-policy configuration | No policy administration operation exists; not proposal-enabled |
  | Non-sensitive custom-role permission edits | Existing atomic permission batch path; not independently approved |
- PostgreSQL migration `032_rbac_independent_approval.sql` and Oracle
  `032_rbac_independent_approval.sql` drafts add proposal persistence, foreign keys, status and
  effective-date checks, idempotency/queue indexes, and payload storage. They have not been
  executed or provider-tested. PostgreSQL wraps DDL in a transaction; Oracle DDL auto-commits and
  needs state-aware forward recovery. Oracle CLOB and PostgreSQL text have the same application
  payload semantics but require provider validation. Never infer deployment readiness from source
  review.
- SHA-256 plus the existing audit hash chain detects payload/audit inconsistencies under the
  current application integrity model; it is not a keyed signature against a database principal
  capable of rewriting both proposal and audit tables. Recomputing SHA-256 after changing the
  payload satisfies the stored-digest check; approval's reconstruction against current role and
  permission rows is an additional application-level consistency check, not protection against
  direct database writes to those rows. The payload uses the default `System.Text.Json`
  representation; a separate standards-based canonical JSON format is not defined. No new
  secret/signing-key configuration was introduced.
  Unauthorized decision attempts are denied but are not persisted as proposal audit events;
  database-backed verification, provider failure injection, full E2E, and independent
  cryptographic anchoring remain open.
- A database writer able to update the proposal payload and digest can recompute a matching digest;
  a principal with write access to RBAC grant tables may bypass the application service. Actual
  database grants were not inspected. Stronger tamper evidence requires a separately approved
  versioned signature/HMAC design with key material protected outside the database, key rotation/
  revocation, verification policy, and independent immutable audit anchoring. Do not add an
  application secret as a substitute for a key-management design.
- Listing lazily marks at most 50 expired pending rows per request, then returns all authorized
  rows without pagination. Additional expired rows therefore remain persisted as `PENDING` until a
  later list/decision call, although the Angular UI derives expiry from each returned timestamp and
  disables actions; approve/reject/cancel independently run the shared decision-time expiry guard.
  The 51-candidate policy unit test exercises the predicate, not service or provider persistence.
  List-triggered expiry records the authenticated caller that triggered lifecycle processing, with a
  reason stating that no human decision was made; no synthetic actor ID is used. Listing-scale and
  audit-latency risks remain.
- The digest is an unkeyed consistency check against accidental or partial payload alteration. It
  does **not** defend against a database principal able to rewrite the proposal row, canonical JSON,
  digest, and audit rows together. The application security boundary assumes database credentials
  remain limited to the application and separately controlled operators. Stronger tamper evidence
  would require an approved signing/HMAC key, protected storage, rotation/versioning policy, and
  independent audit anchoring; none is configured or claimed.
- The 24-case adversarial checklist and evidence status are recorded in the
  [acceptance matrix](./PA_TICKETS_API_E2E_ACCEPTANCE_MATRIX.md). Database-independent tests cover
  pure eligibility, role-ceiling, transition, snapshot, payload-digest, retry, expiry-boundary, and
  UI-state rules. Persisted access decisions, races, transaction/audit rollback, provider DDL, and
  ambiguous commit recovery remain blocked pending approved PostgreSQL/Oracle validation. No
  provider guarantee is inferred from a unit test.
- The migration's case-insensitive identity preflight, canonical indexes, role grants, scope
  backfill, and provider-specific index/DDL behavior still require independent review and provider
  tests. No legacy account transition has been made.

## Baseline architecture and gaps

The following records the codebase inventory before the implementation draft above; it must not be
read as a claim that the draft has passed its required integration or acceptance tests.

- The application already has `PA_TICKETS_ROLE`, `PA_TICKETS_PERMISSION`,
  `PA_TICKETS_ROLE_PERMISSION`, and effective-dated `PA_TICKETS_USER_ROLE` tables. No second
  role-definition table is proposed.
- `PA_TICKETS_ROLE` already has a unique role code, name, description, active state, and
  `N_SYSTEM_ROLE` flag; it can represent custom roles. `PA_TICKETS_USER_ROLE` references role IDs
  without restricting them to a fixed set, and `PA_TICKETS_ROLE_PERMISSION` is the existing
  role-to-permission relation. Thus custom roles do not require a duplicate role table.
- The present schema does **not** itself protect the five built-in codes from application edits,
  guarantee case-insensitive code uniqueness, supply action/module metadata, provide effective
  state on role-permission rows, or capture resource scopes on role memberships. Those are
  application/migration requirements, not capabilities to assume from the current tables.
- Backend services call `IAuthorizationService` with permission-code literals, and the database
  authorization service checks current users, active roles, effective user-role memberships, and
  role-permission rows. Ticket/service-level scope checks exist in addition to those grants.
- `RoleAuthorizationPolicy` still contains role-specific permission allowlists. This prevents a
  role-permission row alone from configuring a new grant and is the principal backend gap.
- Existing permission codes are broad in places (`USER_ADMIN`, `ROLE_ADMIN`, `TICKET_ADMIN`,
  `SERVICE_ADMIN`, `APPROVAL_ADMIN`, and similar); many implemented actions are not individually
  configurable.
- Administration exposes role and permission catalogue reads, but there is no reviewed,
  permission-aware screen/API for changing role-permission mappings.
- Current role assignment code accepts a hard-coded list of role codes, so it rejects custom
  roles despite the schema being able to store them. There is no role create/edit/deactivate UI.
- The proposed Admin Head delegation table and API are explicit and effective-dated. Delegation
  must remain a separate scope fact; neither role names nor reporting-manager links confer it.
- Current Angular routes cover login, home, ticket list/detail/create, and dashboard behavior;
  they do not provide role/permission, SLA, workflow, approval-rule, or notification-rule
  administration screens. Existing backend endpoints/configuration must not be called a delivered
  UI feature.
- JWT role/permission data and Angular guards are presentation hints only. Backend decisions must
  continue to consult current database grants and resource scope.

## Target authorization model

Every protected operation must satisfy all of the following independently:

1. **Authenticated, active principal:** a current active user and at least one current,
   effective, active role membership.
2. **Explicit capability:** the service supplies a stable permission code for the exact action.
   The active permission-catalogue row and its module/action/security metadata must exist.
   Missing, inactive, malformed, or unknown permission metadata denies access.
3. **Role grant:** an active, effective row in `PA_TICKETS_ROLE_PERMISSION` connects one of the
   principal's effective roles to that exact permission.
4. **Resource scope:** a separate scope evaluator confirms the principal may act on the requested
   organization, department, team, Admin group, or individual record.
5. **Domain rules:** workflow state, assigned approver, separation of duties, ownership,
   concurrency, and other operation-specific policies still pass.

The backend evaluates a permission grant and its scope **for the same role membership**. It must
not combine a permission from one role with a broader scope from another. Effective permissions
are read from the database for protected operations; grant and revocation changes therefore take
effect on the next backend authorization decision without waiting for a token to expire.

### Permission metadata and mapping

Reuse `PA_TICKETS_PERMISSION` as the capability catalogue and
`PA_TICKETS_ROLE_PERMISSION` as the configurable grant mapping. The revised migration proposal
adds, provider-appropriately, metadata equivalent to:

- stable permission code, display label, module code, action code, and description;
- read-only/write classification and a non-editable security ceiling/risk class;
- active state and effective-from/effective-to dates for a role-permission grant;
- grant actor, update actor, and update timestamp where not already available.

The PostgreSQL and Oracle 031 drafts now specify provider-specific columns and constraints, but
still require schema/provider review. Existing mapping rows are backfilled as active during
migration; no existing role membership is silently rewritten. The grant table remains the only
role-to-permission mapping. Grant/revoke operations preserve effective-dated rows and append an
audit event with the permission identity, action, actor, time, and supplied reason. The draft does
not add a concurrency-version column or correlation identifier; those remain follow-up audit and
concurrency hardening, not capabilities to claim as implemented. Audit history is not rewritten
or deleted.

Concrete data-model delta proposed for those scripts:

| Existing object | Proposed migration delta |
|---|---|
| `PA_TICKETS_ROLE` | Reuse as-is for role identity and description. Enforce canonical uppercase-code uniqueness. Exactly five built-in rows have `N_SYSTEM_ROLE = true`; custom rows use false. No new role-definition table or custom-role count column. |
| `PA_TICKETS_PERMISSION` | Add module code, action code, scope class, read-only classification, and security/grant class. Metadata is migration-controlled and not editable in the role UI. Backfill known existing codes; unclassified or invalid rows cannot authorize protected requests. |
| `PA_TICKETS_ROLE_PERMISSION` | Preserve its identity and role/permission FKs. Replace permanent pair uniqueness with one active grant per role/permission while retaining inactive historical rows. Add active/effective dates and update actor/time. The draft has no concurrency-version field. |
| `PA_TICKETS_USER_ROLE` | Reuse existing effective-dated memberships; preserve all old rows and user IDs. Add no duplicate membership table. |
| `PA_TICKETS_USER_ROLE_SCOPE` | New only because current role memberships have no explicit scope. FK to a membership and applicable department/team rows; store scope type, effective dates, active state, actor, and reason. |
| `PA_TICKETS_ADMIN_HEAD_DELEGATION` | Reuse the explicit effective-dated Admin Head → Admin relationship; validate both user IDs and memberships and keep reason/change references in the audit event. |
| High-risk RBAC change request | Reuse the approval workflow only if it provides a persisted distinct-second-Super-Admin approval and immutable audit trail. Otherwise add a narrowly scoped pending-change table; never duplicate role or permission definitions. |

The operation in application code requests a **named backend permission code**, not a UI checkbox
or a role name. The permission editor changes mapping data; ordinary grant adjustments do not
require a code change. Adding a genuinely new protected operation still requires implementation
of its server-side check and a reviewed catalogue entry. No wildcard or generic “manage means
everything” grant is allowed. Unknown roles and missing metadata fail closed.

### Built-in and custom role identities

The only **active protected built-in roles** are `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, and
`SPECTATOR`. Their codes are normalized to uppercase and protected by a provider-appropriate
unique index on the normalized code. The migration must abort rather than silently adopt an
existing same-code row if its `N_SYSTEM_ROLE` flag, description, or protected identity is
inconsistent. It allocates new IDs from the existing role sequence, collision-checks every
generated ID, and never assumes IDs 1–5 are available.

`N_SYSTEM_ROLE = true` identifies protected system roles; the target active built-in set is exactly
the five codes above. Retired `OPERATIONS`, `ADMINISTRATOR`, and `AUDITOR` rows remain historical
and reserved, inactive, and unavailable for assignment; their existing system marker is not
reinterpreted as a target built-in identity. Built-in code, name, system-role status, and active
identity cannot be edited or deleted through the application. Super Admin may adjust their
permission mappings only within immutable backend-enforced ceilings; critical invariants
(organization-wide authority limited to Super Admin, Admin Head delegation, and Spectator
deny-overrides) are not editable in the permission UI.

Custom roles use ordinary rows in `PA_TICKETS_ROLE` with `N_SYSTEM_ROLE = false` and mappings in
`PA_TICKETS_ROLE_PERMISSION`. Super Admin can create them, edit their description, configure
permitted grants, assign them to users, and deactivate them. The role code is immutable after
creation, must be normalized uppercase and satisfy the reviewed code syntax, and may not collide
with any built-in or retired role code. Validation also rejects separator/case aliases of built-in
and retired codes (for example `SUPERADMIN` or `SUPER-ADMIN`) and the immutable permission ceiling
prevents a custom role from recreating a built-in role's protected authority under another name.
Examples such as `SLA_MANAGER`,
`TICKET_COORDINATOR`, and `POLICY_REVIEWER` are valid custom names. There is no product-imposed
maximum number of custom roles; normal database constraints, pagination, and abuse-rate controls
still apply. Deactivation previews affected users, disables the role, closes its current/future
memberships at the approved instant, and audits each effective change. Reactivation does not
silently restore closed memberships.

### Resource scopes (separate from module capabilities)

Permission mappings answer “may this role perform this action?” They do not define data scope.

| Scope | Source of authority |
|---|---|
| Organization | `SUPER_ADMIN` only, subject to the specific permission and domain rules. |
| Department | The effective department relationship and the target resource's department. An `ADMIN` is limited to its authorized department. |
| Team | Effective user/team membership and the target resource's team; department/team consistency is checked server-side. |
| Assigned Admin group | An active, effective `PA_TICKETS_ADMIN_HEAD_DELEGATION` from that Admin Head user ID to that Admin user ID. It grants only the documented Admin-management actions and never general organization scope. |
| Individual record | The record is loaded from the database and checked by its service-specific authorizer (ticket ownership, assignment, client membership, approver assignment, etc.). Client-supplied IDs or JWT claims are not proof of scope. |

Where the existing department/team/client/assignment relationships are sufficient, use those
relationships rather than copying scope into a role-permission row. Any additional explicit
scope-assignment table must be separately justified, effective-dated, auditable, and must not
duplicate role definitions.

The existing user-role row has no scope fields. The proposed migration therefore adds one
effective-dated `PA_TICKETS_USER_ROLE_SCOPE` relation keyed to a specific user-role membership for
explicit organization/department/team scope that cannot be derived safely from current user/team
relationships. This is a justified scope table, not a duplicate role or permission definition.
The row has a scope type, optional department/team keys, valid-from/to, active state, actor,
reason, and foreign keys to the membership and applicable organization rows. A check constraint
enforces valid scope-key combinations. Organization scope is grantable only through a protected
Super Admin flow. The existing Admin Head delegation remains the source for assigned-Admin-group
scope; individual ticket/work-item/approval scope remains resolved by domain authorizers and
existing record relationships rather than a generic unvalidated record-ID grant. The service
validates scope type, ownership, hierarchy consistency, grantor authority, and requested resource
on every write and protected operation. Organization-wide scope is never conferred by custom role
name alone.

### Multiple role memberships and deny precedence

- Capability grants are unioned only across independently valid role-membership/scope decisions;
  the scope from one role cannot widen a grant from another.
- `USER` may accompany a scoped `ADMIN` or `ADMIN_HEAD` membership. The latter roles do not gain
  organization-wide scope from the combination.
- A custom role may accompany `USER` or another custom role. Each action is authorized using the
  particular membership that grants it and that membership's own effective scope; combinations
  cannot combine a broad permission from one role with scope from another.
- `ADMIN` and `ADMIN_HEAD` are mutually exclusive for the same effective period; the Head role
  already includes ordinary Admin capabilities and adds only explicitly delegated Admin
  management.
- `SUPER_ADMIN` is exclusive of other effective role memberships, including custom roles.
  Privileged role assignment and changes to the final effective Super Admin remain controlled,
  separately audited workflows.
- Any active effective `SPECTATOR` membership is a deny-overrides veto for every mutation, even
  if another membership grants a write capability. Its reads remain limited by that membership's
  resource scope. No non-GET/HEAD/OPTIONS API method is implicitly safe for Spectator.
- Old role codes are transition-only. After reviewed user migration they are inactive and cannot
  be newly assigned; an unknown or inactive role never grants authority.

## Module and action catalogue proposal

This is the proposed catalogue for implemented PA TICKETS modules. Each named action is a distinct
backend permission code; View/Create/Edit/Manage are UI groupings, not wildcard permissions.
“Delete” is intentionally absent where the application has no supported hard-delete operation;
retire/deactivate is used only where the module already supports that lifecycle. The final
catalogue must be reconciled against the controller/service inventory and every protected route
before migration approval.

| Module | Explicit action permissions | Initial role/scope policy notes |
|---|---|---|
| Profile and authentication | `PROFILE_VIEW_SELF`, `PROFILE_EDIT_SELF`, `PROFILE_PASSWORD_CHANGE` | Self-only; credential changes follow authentication policy. No role grants access to another user's credentials. |
| Tickets | `TICKET_READ`, `TICKET_CREATE`, `TICKET_EDIT`, `TICKET_TRANSITION`, `TICKET_ASSIGN` | Individual ticket plus department/team/client/assignee scope. `TICKET_UPDATE` is a legacy alias to split during code migration. No hard-delete permission unless a real endpoint and retention policy are approved. |
| Work items | `WORK_ITEM_READ`, `WORK_ITEM_CREATE`, `WORK_ITEM_ASSIGN`, `WORK_ITEM_COMPLETE` | Ticket/work-item scope and current assignment/workflow checks remain mandatory. |
| Incident, problem, change | `INCIDENT_READ`, `INCIDENT_CREATE`, `INCIDENT_EDIT`, `INCIDENT_ASSIGN`; `PROBLEM_READ`, `PROBLEM_CREATE`, `PROBLEM_EDIT`, `PROBLEM_ASSIGN`; `CHANGE_READ`, `CHANGE_CREATE`, `CHANGE_EDIT` | Related-ticket access and lifecycle policies remain mandatory. |
| Attachments, evidence, relations | `ATTACHMENT_READ`, `ATTACHMENT_CREATE`, `ATTACHMENT_VERSION_CREATE`; `EVIDENCE_READ`, `EVIDENCE_CREATE`; `RELATION_READ`, `RELATION_CREATE` | Each operation requires authorization to its persisted ticket. No delete/remove grant without a supported endpoint and audit/retention design. |
| Users and organizational structure | `USER_READ`, `USER_CREATE`, `USER_EDIT`, `USER_DEACTIVATE`, `USER_PASSWORD_RESET`, `USER_ROLE_ASSIGN`; `DEPARTMENT_READ`, `DEPARTMENT_CREATE`, `DEPARTMENT_EDIT`, `DEPARTMENT_DEACTIVATE`; `TEAM_READ`, `TEAM_CREATE`, `TEAM_EDIT`, `TEAM_DEACTIVATE`; `CLIENT_READ`, `USER_CLIENT_ASSIGN` | Admin actions are department-scoped unless explicitly Super Admin-only; password/role changes have additional safeguards. |
| Roles and permission governance | `ROLE_READ`, `PERMISSION_CATALOG_READ`, `ROLE_PERMISSION_MANAGE`, `ADMIN_HEAD_DELEGATION_ASSIGN`, `ADMIN_HEAD_DELEGATION_REVOKE` | Permission-mapping changes are Super Admin-only under the proposed ceiling and audited workflow. Admin Head delegation is separately scoped and audited. |
| Ticket metadata | `TICKET_TYPE_READ`; `STATUS_READ`, `STATUS_CREATE`, `STATUS_EDIT`, `STATUS_TRANSITION_MANAGE`; `PRIORITY_READ`, `PRIORITY_CREATE`, `PRIORITY_EDIT` | Configuration actions are not ordinary ticket update rights. |
| Service/category/request-type catalogue and dynamic forms | `SERVICE_READ`, `SERVICE_CREATE`, `SERVICE_EDIT`, `SERVICE_RETIRE`; `CATEGORY_READ`, `CATEGORY_CREATE`, `CATEGORY_EDIT`, `CATEGORY_RETIRE`; `SUBCATEGORY_READ`, `SUBCATEGORY_CREATE`, `SUBCATEGORY_EDIT`, `SUBCATEGORY_RETIRE`; `REQUEST_TYPE_READ`, `REQUEST_TYPE_CREATE`, `REQUEST_TYPE_EDIT`, `REQUEST_TYPE_PUBLISH`, `REQUEST_TYPE_RETIRE`; `FORM_FIELD_READ`, `FORM_FIELD_CREATE`, `FORM_FIELD_EDIT`, `FORM_FIELD_OPTION_EDIT` | Existing effective-window, publication, association, and form-integrity checks remain. |
| Workflow | `WORKFLOW_READ`, `WORKFLOW_CREATE`, `WORKFLOW_EDIT`, `WORKFLOW_PUBLISH`, `WORKFLOW_RETIRE`, `WORKFLOW_INSTANCE_READ`, `WORKFLOW_INSTANCE_CREATE` | Definition rights do not bypass instance assignment, version, ticket, or separation-of-duties checks. |
| Approvals | `APPROVAL_RULE_READ`, `APPROVAL_RULE_CREATE`, `APPROVAL_RULE_EDIT`, `APPROVAL_RULE_PUBLISH`, `APPROVAL_RULE_RETIRE`; `APPROVAL_INSTANCE_READ`, `APPROVAL_INSTANCE_CREATE`, `APPROVAL_INSTANCE_APPROVE`, `APPROVAL_INSTANCE_REJECT` | An action permission never substitutes for the effective approver assignment or separation-of-duties rules. |
| SLA | `SLA_READ`, `SLA_POLICY_CREATE`, `SLA_POLICY_EDIT`, `SLA_POLICY_PUBLISH`, `SLA_POLICY_RETIRE`, `SLA_CALENDAR_CREATE`, `SLA_CALENDAR_EDIT` | Effective policy/version and operational lifecycle checks remain. |
| Communication and notification | `COMMUNICATION_READ`, `COMMUNICATION_PROVIDER_EDIT`, `COMMUNICATION_CHANNEL_EDIT`, `COMMUNICATION_TEMPLATE_CREATE`, `COMMUNICATION_TEMPLATE_EDIT`, `COMMUNICATION_TEMPLATE_PUBLISH`, `COMMUNICATION_TEMPLATE_RETIRE`; `NOTIFICATION_RULE_READ`, `NOTIFICATION_RULE_CREATE`, `NOTIFICATION_RULE_EDIT`, `NOTIFICATION_RULE_PUBLISH`, `NOTIFICATION_RULE_RETIRE` | Delivery/provider state and effective-window checks remain. Publishing/retiring are not implied by editing. |
| Reports, dashboards, audit | `REPORT_READ`, `REPORT_EXPORT`, `DASHBOARD_READ`, `KPI_READ`, `AUDIT_READ`, `AUDIT_EXPORT` | Every query/export is additionally scope-filtered. Export is a distinct data-egress action, not implied by View. |
| Global configuration | `CONFIG_READ`, `CONFIG_EDIT` | Organization-wide configuration remains Super Admin-only; there is no general configuration delete. |

The action list is a **proposal**, not a claim that each new code is already enforced by the
current application. The implementation phase must map each listed capability to actual
controller/service operations, split broad legacy checks, add a code-catalogue parity test, and
remove deprecated aliases only after all call sites are migrated.

## Role defaults and grant ceilings

The role editor is configurable, but not an unrestricted privilege-escalation console.
The catalogue stores immutable-by-UI security metadata (read/write, sensitivity, and minimum
grant class). Backend code also enforces maximum grant classes; changing a role mapping cannot
turn a department action into an organization-wide action or bypass domain separation of duties.
Only migration/controlled code review changes a permission's security classification.

| Role | Proposed starting grants | Maximum scope / restrictions |
|---|---|---|
| `USER` | Ticket read/create/edit; assigned work-item read/create/complete; scoped attachment/evidence actions; self-profile actions. | Own authorized team/client/tickets and individual assigned records. No user/role/configuration governance. |
| `ADMIN` | User directory/create/edit/deactivate and limited USER/SPECTATOR membership operations; ticket operations needed for department administration. | Explicit department and its teams/resources. Cannot grant ADMIN, ADMIN_HEAD, SUPER_ADMIN, or role permissions. |
| `ADMIN_HEAD` | ADMIN baseline plus the specific Admin-management actions needed for assigned Admins. | Department scope plus only Admin user IDs with a current explicit delegation. Cannot grant ADMIN_HEAD/SUPER_ADMIN or change permission mappings. |
| `SUPER_ADMIN` | Organization-wide administration and permission-governance capabilities from the reviewed catalogue. | Organization scope, still subject to approval/assignment/separation-of-duties rules. Does not gain an approval decision solely from the role name. |
| `SPECTATOR` | Approved read-only capabilities; no export by default. | Same resource scope rules as the underlying authorized data. Any mutation is denied by backend deny-overrides policy and the API method guard. |
| Custom role | Only the explicit module/action grants selected by Super Admin, such as scoped SLA policy management or ticket coordination. | Scope is separately assigned and validated. No role-governance, built-in identity, Super Admin, or Spectator-invariant bypass. No fixed role-count ceiling. |

These are seed defaults, not immutable grants. Authorized Super Admins may configure supported
role-permission mappings inside their ceiling. The initial mapping of `AUDIT_EXPORT` to
`SPECTATOR` is deliberately withheld; preserving legacy Auditor export access requires an
explicitly reviewed decision because it exposes data outside ordinary on-screen viewing.

## Review of the current 34-code Super Admin allowlist

The current proposal's 34 codes cover these broad families:

- approvals: `APPROVAL_ADMIN`, `APPROVAL_PUBLISH`, `APPROVAL_READ`;
- audit: `AUDIT_EXPORT`, `AUDITOR_VIEW`;
- catalogues: `CATALOGUE_PUBLISH`, `CATEGORY_ADMIN`, `CATEGORY_READ`,
  `REQUEST_TYPE_ADMIN`, `REQUEST_TYPE_READ`, `SERVICE_ADMIN`, `SERVICE_READ`;
- clients: `CLIENT_ADMIN`;
- communication: `COMMUNICATION_ADMIN`, `COMMUNICATION_PUBLISH`, `COMMUNICATION_READ`,
  `COMMUNICATION_RETIRE`;
- configuration: `CONFIG_ADMIN`;
- notifications: `NOTIFICATION_ADMIN`, `NOTIFICATION_PUBLISH`, `NOTIFICATION_READ`,
  `NOTIFICATION_RETIRE`;
- roles/users: `ROLE_ADMIN`, `USER_ADMIN`;
- SLA: `SLA_ADMIN`, `SLA_PUBLISH`, `SLA_READ`, `SLA_RETIRE`;
- tickets: `TICKET_ADMIN`, `TICKET_READ`, `TICKET_UPDATE`;
- workflows: `WORKFLOW_ADMIN`, `WORKFLOW_PUBLISH`, `WORKFLOW_READ`.

This list is not a complete action catalogue. It lacks distinct user create/edit/deactivate,
password reset, role assignment vs permission mapping, explicit Admin Head delegation, department
and team lifecycle, ticket create/edit/assign/transition, work-item/attachment/evidence actions,
separate approval decisions, request-type edit/retire distinctions, and report export beyond audit.
Several `*_ADMIN` codes conflate view and multiple mutations. Therefore “all 34 codes” is not a
sufficient Super Admin model. The replacement Super Admin grant is the complete **approved
capability catalogue** with explicit operation-level and scope checks, not a hard-coded 34-code
array and not a role-name bypass.

## Permission administration UI and API

Proposed Angular screen: **Administration → Roles → Permissions**.

- List the five protected built-in roles and any active custom roles, clearly distinguishing them.
  Retired legacy role codes are not assignable.
- Provide custom-role create, description edit, permission edit, assignment, and deactivation
  flows. Codes are immutable after creation; no arbitrary custom-role count limit is imposed.
- Protect built-in code/name/system flag/active identity from edit or delete actions. Built-in
  permission changes remain subject to the backend ceilings and critical invariants.
- Show modules, explicit actions, current grant, security classification, scope, and affected
  active membership count where that count can be obtained safely.
- Fail closed: absent/inactive permission metadata, unknown action codes, or missing route
  permission metadata hides the action and blocks protected navigation. UI visibility is never
  authorization.
- Show a before/after diff, scope impact, affected role, effective time, reason/change reference,
  and explicit confirmation before submit. High-risk changes require a second distinct active
  Super Admin approval; the requester cannot approve their own change.
- Do not allow a user to change their own role membership, grant themselves access, modify their
  own Super Admin governance mapping, or remove the final effective permission-governance path.
- Prevent grants above the permission's immutable grant ceiling. Super Admin-only permissions
  cannot be assigned to ordinary roles through the screen.
- Custom-role grants may include scoped module administration (for example SLA management) when
  permitted by the capability's security class; they cannot include role/permission governance,
  built-in Super Admin authority, or mutations inconsistent with Spectator restrictions.
- The API validates all submitted permission codes against the active server/database catalogue,
  role ceiling, target role state, effective dates, and approver separation-of-duties. It performs
  the change and audit append in one transaction.
- The permission snapshot used for navigation is fetched from the authenticated API and refreshed
  after a change and on subsequent navigation/session refresh. Stale UI state may hide a newly
  granted action, but cannot authorize an API operation; revocations are enforced immediately by
  the backend database check.

Proposed API surface is versioned under administration, for example:

- `POST /api/v1/admin/roles` (custom role only)
- `PATCH /api/v1/admin/roles/{roleId}` (description only for custom roles)
- `POST /api/v1/admin/roles/{roleId}/deactivate` (custom roles only, with impact preview)
- `POST /api/v1/admin/users/{userId}/roles` and a corresponding effective-dated revoke operation
  (custom role assignment is Super Admin-only)
- `GET /api/v1/admin/roles/{roleId}/permissions`
- `PUT /api/v1/admin/roles/{roleId}/permissions/{permissionCode}` with effective date, reason,
  and expected row version
- scope grant/revoke operations keyed by the exact user-role membership
- a separate request/approval endpoint for high-risk changes
- `GET /api/v1/auth/permissions` for the caller's current presentation snapshot

The exact contract is subject to controller conventions and the implementation review.

The current routes do not provide admin screens for SLA, workflow, approval-rule, notification,
or business-policy configuration. The implementation plan must add or integrate separate,
authorized screens for the existing supported APIs and preserve each domain's own lifecycle:
SLA policy/calendar versioning, workflow definition validation/publication, approval-rule
publication and instance decisions, and notification rule publication/retirement. These remain
separate from RBAC role-permission data. A screen is not evidence that missing backend business
operations (such as unsupported hard delete) exist.

## Admin Head delegation interface and audit

The delegation interface selects an Admin Head and Admin by database user IDs, displays validated
identities/scope, accepts valid-from/valid-to, active state, reason/change reference, and requires
confirmation. Backend creation verifies both accounts are active and that each effective role
membership covers the full requested interval. Revocation is soft/inactive and preserves the
planned range when cancelled before its start. Reassignment never relies on reporting-manager data.

Every create, revoke, change, failed privileged attempt, and approval records actor, target IDs,
old/new dates/status, reason/change reference, timestamp, and correlation ID in the existing
append-only hash-chained audit system. Entity identifiers are namespaced to prevent unrelated
administration records from sharing an audit chain. Every delegated operation reloads current
delegation and Admin membership and checks the requested resource/scope in the same backend
authorization decision.

## Existing role/member mapping plan

Do not infer a user's intended job from a legacy role label:

- `OPERATIONS` → evaluate each membership with its owner. Map to `ADMIN` only when an approved
  department and delegated authority are confirmed; otherwise map to `USER` or a custom role such
  as `TICKET_COORDINATOR` with explicit ticket capabilities and scope. Never map automatically to
  `SUPER_ADMIN`.
- `AUDITOR` → proposed target is `SPECTATOR` for scoped read access, or a custom role such as
  `POLICY_REVIEWER` if that exact review capability is needed. Existing export access is not
  silently carried over; approve a scoped `AUDIT_EXPORT` grant or record the intentional
  reduction before transition.
- `ADMINISTRATOR` → remains a separate transition. No mapping or account change for user 9001
  without owner identity confirmation and explicit separate approval.

For every effective or future-effective legacy membership, an owner-approved row-level plan must
identify source membership ID, target role, target scope, effective instant, reason/change
reference, reviewer, and audit verification. The application transition closes the old membership
and inserts a new dated membership in one transaction using existing user IDs; it does not rewrite
historical memberships, ticket ownership, audit history, or department/team relations. If any
membership is unreviewed, migration finalization fails closed and the old role remains in
transition state rather than silently disabling users.

Finalization disables legacy role definitions only after the application reports zero active or
future-effective memberships against them. Historical role rows and closed memberships remain
for audit. Until that gate passes, legacy roles are transition-only and hidden from ordinary role
assignment UI.

## PostgreSQL and Oracle migration plan

No migration script is approved by this design document. The current 030 files must be revised
into the following reviewed provider-specific sequence:

| Script stage | Oracle artifact | PostgreSQL artifact | Purpose |
|---|---|---|---|
| Configurable RBAC schema/catalogue | `database/migrations/030_five_role_rbac.sql` (revise) | `database/migrations/postgresql/030_five_role_rbac.sql` (revise) | Add permission metadata, effective mapping fields, explicit scope/delegation structures, five built-ins, and initial capability catalogue/mappings. Existing draft contents are superseded. |
| Reviewed legacy membership mapping | `database/migrations/031_rbac_legacy_membership_mapping.sql` (prepare after account review) | `database/migrations/postgresql/031_rbac_legacy_membership_mapping.sql` (prepare after account review) | Application-driven, per-membership closure/creation and audit from an approved mapping manifest; no blanket role conversion. |
| Legacy role retirement | `database/migrations/032_rbac_retire_legacy_roles.sql` (prepare) | `database/migrations/postgresql/032_rbac_retire_legacy_roles.sql` (prepare) | Guarded inactivation only after zero active/future memberships and separate Administrator transition approval. |

These paths describe the intended review package; only the 030 v1 files currently exist, and
their first-pass SQL is explicitly marked superseded. The 031/032 scripts are not yet created.
No replacement executable migration is being represented as ready in this design phase.

1. **Preflight (read-only):** verify provider, database and role identity; inspect role codes/IDs,
   sequence state, permission catalog, current mappings, active/future memberships, constraints,
   and row counts. Abort on any identity mismatch, unexpected permission catalog, conflicting
   duplicate role code (including case variants), sequence/constraint mismatch, or unreviewed
   legacy membership. Never run Oracle SQL for this exercise.
2. **030 configurable catalogue and schema:** idempotently add metadata/effective-state columns
   to the existing permission and role-permission tables; seed explicit capability rows and
   metadata; create the five built-ins using the existing sequence with collision checks and
   canonical-code uniqueness; mark them as system roles; allow custom-role rows without a count
   cap; add reviewed initial role-permission mappings; create the effective-dated role-scope and
   Admin Head delegation relations. Reuse the existing approval workflow for high-risk permission
   changes only if it provides equivalent distinct-actor approval and an auditable change request;
   otherwise add a narrowly scoped pending-RBAC-change relation, not a second role or permission
   table. Preserve existing roles and memberships in this phase. Built-in insertion is idempotent
   by normalized code; if an existing same-code row is not the expected system role, abort for
   human resolution rather than creating a duplicate.
3. **Application rollout:** deploy backend checks, scoped delegation, permission administration
   API/UI, and audit behavior only after all required metadata/capabilities are present. Missing
   metadata denies. Keep transitional legacy behavior explicitly time-bounded and prevent new
   legacy role assignments.
4. **031 reviewed membership transition:** use an application-level, audited batch driven only by
   individually owner-approved mappings. It closes old effective memberships and adds new
   memberships at the approved instant. It does not create Super Admin accounts; user 9001 remains
   out of scope until separately approved. This stage records the exact old-membership ID, target
   role ID/code, target scope, effective instant, owner/reviewer, reason, and audit correlation.
   No blanket OPERATIONS/AUDITOR conversion is permitted.
5. **032 legacy-role finalization:** a separate guarded migration checks for zero active and
   future-effective memberships for `OPERATIONS` and `AUDITOR` (and `ADMINISTRATOR` only after its
   separate transition approval), then marks the definitions inactive. It never deletes role
   definitions, membership history, or audit records. All three legacy codes are reserved against
   custom-role creation; retired codes cannot be reactivated as custom roles.
6. **Post-migration verification:** compare role/permission/membership counts and stable user,
   ticket, audit, department, and team identifiers to the preflight snapshot; verify audit chain;
   run authorization and UI effective-change tests; verify idempotent rerun in a disposable clone.

PostgreSQL changes must be transaction-wrapped and safe to rerun. Oracle DDL commits implicitly;
the Oracle script needs object-existence guards, explicit partial-run recovery instructions, and
must be rehearsed on a disposable Oracle clone by a separately authorized operator. Sequence
allocation uses existing sequences and loops past occupied primary keys; no numeric ID is
assumed. Never reset sequences or drop objects during rollback.

Rollback is compensating and audited: stop new permission changes, restore reviewed mappings from
the audit/change record, close new role memberships, create corrective dated memberships, and
retain all rows and history. Do not drop role/permission/delegation objects and do not revert to a
legacy global authorization bypass.

## Verification plan after design approval

No DB-backed write test may run before the explicit execution gate. After approval, use only
`pa_tickets_h_verify` as `pa_tickets_test_user`, verify that identity immediately before and after
each database-writing batch, and never use shared `pa_tickets_test`, Oracle, or the prohibited
provisioning script.

1. **Catalogue and migrations:** required code/metadata parity; all 34 existing codes classified;
   five built-in canonical identities; case-variant duplicate rejection; custom-role create/edit/
   assign/deactivate with no configured fixed count; role-scope FK/date validation;
   generated-ID collisions; existing role-ID preservation; FK/unique/check constraints; idempotent
   rerun; PostgreSQL transaction rollback; Oracle script review/rehearsal only under separate
   authorization; no changes to existing users/tickets/audit rows.
2. **Authorization matrix:** for every module/action, exercise allowed and denied calls for all
   five roles, missing/inactive permission metadata, unknown role code, removed mappings, future
   and expired mappings, inactive user/role/membership, and stale JWT/UI permission snapshots.
3. **Scope and role combinations:** organization/department/team/assigned-Admin/individual record;
   same- and cross-department/team; Admin Head assigned/unassigned Admin; role grant/scope from
   different memberships cannot combine; ADMIN+USER, ADMIN_HEAD+USER, SUPER_ADMIN exclusivity,
   and Spectator deny-overrides across direct HTTP mutation calls.
4. **Governance:** no self-role/self-permission escalation; Admin cannot grant Admin; Head cannot
   assign unapproved Admins; only Super Admin permission governance; built-in identity cannot be
   edited/deleted; custom-role security ceilings; high-risk second-actor
   approval, confirmation cancellation, audit reason, expected-version/concurrency conflicts,
   and final-governance-path protections.
5. **Compatibility/data:** per-user OPERATIONS/AUDITOR mapping; separate ADMINISTRATOR transition
   gate; Auditor export decision; all IDs, ticket ownership, org links, effective intervals, and
   historical audit hashes preserved.
6. **Angular real browser:** permission editor visibility and save flow; missing metadata fails
   closed; custom-role CRUD/assignment/deactivation; successful mapping change refreshes UI;
   revoked access disappears; direct API denial remains authoritative; explicit delegation
   create/revoke and scope; separate SLA/workflow/approval/notification configuration flows are
   tested only where those backend operations exist; real Angular-to-API-to-PostgreSQL login and
   ticket flow.
7. **Acceptance matrix:** update every listed API route individually with actual status/evidence;
   no subset, anonymous denial, build, or mocked UI test is counted as full acceptance.

## Risks and unresolved decisions

- The exact per-user migration map for OPERATIONS/AUDITOR and the legacy Administrator owner are
  not known from the redacted inventory. Finalization cannot safely proceed without them.
- The schema can already store custom roles, but case-insensitive identity protection, backend
  support for configurable grants, custom-role administration, role scopes, and UI are missing.
  Adding role rows alone would create unsafe, nonfunctional “custom roles.”
- The current Angular route tree has no configuration UI for SLA, workflow, approval rules, or
  notification rules. Backend API/configuration behavior and front-end delivery must be tracked
  separately; this design does not claim those screens or any missing business operation exist.
- Auditor export is a distinct data-egress capability. Keeping it for all Spectators may be too
  broad; removing it may be a behavior change. Obtain a business-owner decision.
- The current 36 distinct permission codes used by backend authorization call sites do not express all operation-level actions. Their
  split requires coordinated service/controller/UI/test changes; a schema-only migration is not
  a secure configurable-RBAC implementation.
- Two-person approval requires an available second Super Admin or an explicitly approved
  out-of-band recovery procedure. Do not weaken it when only one bootstrap administrator exists.
- `PA_TICKETS_ROLE_PERMISSION` uniqueness and provider-specific column differences need exact
  constraint/schema inspection before final SQL is written. Existing role-permission data must
  remain attributable and auditable.
- Organization/team/record scoping is distributed across existing services. Every route must be
  inventoried and bound to a server-side resource loader/authorizer; a permission screen alone
  cannot close those gaps.
- Browser acceptance remains blocked until an approved actor, migration, and safe test records are
  available. No production-readiness claim is supported.

## Continuation review — migration, audit, and configuration-domain gaps

The following is a source-level review only. No database was accessed, no migration was run, and
no account or membership was changed.

### Migration 031 independent draft review

- **Identity and allocation:** the PostgreSQL and Oracle drafts preserve existing role/user IDs,
  seed built-ins by stable code, and allocate role, grant, scope, and delegation IDs from existing
  sequences while skipping occupied IDs. Existing `OPERATIONS`, `ADMINISTRATOR`, and `AUDITOR`
  definitions and memberships are not mapped or rewritten. There is no creation of a Super Admin
  membership. Existing canonical-code conflicts are intended to abort.
- **Idempotency:** DDL/column/index creation and seed rows are guarded, and active role-permission
  uniqueness is provider-specific (PostgreSQL partial index; Oracle function-based unique index).
  Scope backfill now considers only active, not-yet-expired memberships, starts a new scope no
  earlier than migration time, and skips an equivalent currently effective scope on re-execution.
  This avoids asserting that a present-day department/team was historically in force for the
  entire old membership. Re-execution and both providers remain untested.
- **Data and references:** the draft retains historical role memberships, role-permission rows,
  user IDs, ticket data, and audit rows. The added scope table references memberships and existing
  department/team/client rows; delegation references existing users. Organization and record
  identifiers are intentionally not backed by foreign keys because no corresponding common
  organization/record table is established by this migration. Existing permission rows receive
  metadata updates by code, so their exact pre-migration metadata/grants need a read-only
  production-like diff before approval.
- **Provider differences:** PostgreSQL uses a single transaction, `IF NOT EXISTS`, boolean columns,
  catalog-checked constraints, and partial indexes. Oracle uses anonymous PL/SQL blocks, catalog
  checks, numeric booleans, and function-based indexes. Oracle DDL implicitly commits; a failed run
  can leave earlier columns, tables, or indexes in place. Oracle recovery must be forward-only:
  inspect actual objects, repair the failed stage, then rerun only after a reviewed state-aware
  runbook is prepared. Do not attempt a blanket rollback.
- **Known migration gates:** both drafts assume baseline role/permission/user-role sequences and
  tables exist with the named columns. Oracle's `CREATE TABLE IF MISSING` guards do not reconcile a
  partially existing table or constraint definition. Neither script has automated migration
  tests, provider rehearsal, rollback/recovery rehearsal, deployment lock, nor a signed
  preflight/postflight manifest. Those are blockers, not passed criteria.
- **Scope/delegation integrity:** service methods use serializable transactions and overlap checks,
  while the migration's unique delegation key only prevents duplicate start instants, not arbitrary
  overlapping intervals. Concurrency behavior must be verified independently on each provider.
  The migration does not backfill Admin Head-to-Admin delegation; that relationship must be
  explicitly assigned and audited after the application/migration gate.

### Current configurable-RBAC completion gaps

- Membership revocation now also closes active membership scope rows and appends before/after
  audit values for the membership and each scope. Custom-role deactivation and direct scope removal
  likewise record old/new status and effective dates. A PostgreSQL regression assertion was added
  for role-membership removal audit values, but it was not run because database access is prohibited
  for this continuation.
- Built-in grant seeding remains deliberately conservative and the permission catalogue is not
  proven to be a one-to-one inventory of every protected API operation. Ordinary mappings still
  use the atomic batch; grants to protected built-ins other than `SUPER_ADMIN` now use the proposal
  workflow described above. New `SUPER_ADMIN` grants remain blocked; revocations remain available.
- The batch endpoint applies one actor/reason to all included changes, checks the
  expected set of currently effective permission IDs, validates every grant before writing, and
  stages changes plus audit events in one serializable transaction. Each audit row is saved inside
  that transaction before the next row is chained. Stale snapshots are checked in source; concurrent
  stale-snapshot rejection depends on provider transaction behavior and remains unverified. This
  applies ordinary mapping changes plus audit in one serializable transaction. Sensitive permission
  grants and all grants to protected built-ins other than `SUPER_ADMIN` are diverted to the
  independent proposal workflow above; direct single-item and batch grant endpoints reject those
  changes. `ORGANIZATION_ADMIN` and `SECURITY_GOVERNANCE` grants remain blocked by immutable
  ceilings. Admin Head delegation create/end service operations, role-scope grants, and
  `ADMIN`/`ADMIN_HEAD` membership grants remain disabled. The Angular page exposes delegation
  history read-only and provides the permission proposal queue.
- There is still no automated route-to-permission parity test; source inventory remains a required
  security deliverable. Proposal application is implemented only for role-permission grants;
  membership, scope, delegation, policy-configuration, and `SUPER_ADMIN` grant paths remain
  fail-closed and require separate reviewed handlers before any restriction is removed.
- Existing backend configuration domains were found for SLA policies/calendars/escalation,
  workflow definitions and versions, approval rules and versions, communication templates and
  provider/channel settings, notification rules, catalogue/request types, and operational
  status/priority/transition metadata. Their controller/service lifecycles and validation are not
  evidence of an Angular administration screen. No domain-specific Angular screens were added in
  this continuation. Business-policy versioning/publishing and any unsupported hard-delete or
  provider-delivery behavior remain unimplemented unless exposed by those existing domain APIs.
- Domain-by-domain UI inventory:

  | Domain | Backend status | Angular administration UI | Current classification |
  |---|---|---|---|
  | SLA policies, calendars, escalation | APIs/services exist for definitions, versions, and escalation configuration | Not found | Backend available; UI missing |
  | Workflow definitions | APIs/services support draft, validation, publish/retire, and runtime history | Not found | Backend available; UI missing |
  | Approval rules | APIs/services support versioned rule administration and runtime history/decisions | Not found | Backend available; UI missing |
  | Notifications and communications | APIs/services expose rules, templates, channels/providers, publication/retirement, and audit | Not found | Backend available; UI missing |
  | Service/category/request-type catalogue | Backend APIs exist; ticket creation consumes catalogue metadata | Ticket creation consumer exists; administration screens not found | Partially implemented |
  | Operational status/priority/transitions | Backend administration API/service exists | Not found | Backend available; UI missing |
  | Business policies/global configuration | No complete independent domain contract or administration flow established by this review | Not found | Not implemented / requirement blocked |
- **Permission/API parity:** the repository has controller-route inventory, but no verified,
  definitive route-to-permission/action/scope matrix or automated parity test that proves every
  protected operation is classified. Existing checks are distributed among services and
  controllers; a grep for controller-level checks alone is not sufficient because some routes
  delegate authorization to services. This deliverable is therefore **incomplete**: before
  release, enumerate all 226 routes against actual call paths, classify HTTP mutation behavior,
  then add a test enforcing explicit metadata and valid permission-code mappings. Do not infer
  full backend coverage from the role catalogue.
- Before release, inventory every protected endpoint against an atomic permission/action/scope
  matrix, then add and test permission-aware configuration screens only for operations the existing
  API contracts actually support. Do not infer that an RBAC screen provides SLA/workflow/policy
  configuration.

### Authenticated acceptance plan and status

No live API request, browser login, database query, or migration execution was performed in this
continuation. Authenticated route and browser acceptance remains **BLOCKED**, not passed. Once
separately authorized, use the approved disposable database identity and a reviewed, non-privileged
fixture; test each route individually and record actor role, permission/scope, request outcome,
persisted state, and audit result. Cover USER, ADMIN, SUPER_ADMIN, ADMIN_HEAD (assigned and
unassigned Admin), SPECTATOR, and a representative custom role. Include direct mutation requests
as well as UI actions, cross-department/team/resource records, membership/permission effective
dates, revocation, stale UI/token behavior, and audit-chain verification. The Super Admin actor and
any privileged membership transition require their own approved bootstrap plan; do not create them
as part of test setup.
