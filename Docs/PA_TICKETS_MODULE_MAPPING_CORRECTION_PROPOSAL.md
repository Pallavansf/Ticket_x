# PA_TICKETS module mapping correction proposal

Proposal date: 2026-10-16. This document converts the source-only findings in `Docs/PA_TICKETS_MODULE_MAPPING_AUDIT.md` into reviewable corrections. It does not approve new permission identifiers, alter role grants, change enforcement, modify Angular behavior, or execute migrations.

## Confirmed defects versus decisions

| Finding | Classification | Evidence | Smallest safe correction |
|---|---|---|---|
| Operational metadata was initially suspected to use the wrong module permission | Resolved audit interpretation; no implementation defect confirmed | `OperationalMetadataRepository.cs`; `OperationalMetadataAdministrationController.cs`; migration 031 `CONFIG_ADMIN` | Decide whether each operation is ticket configuration or protected configuration, then align one existing/approved permission code across service, UI, and catalogue. Do not change it in this phase. |
| Reports, dashboards, KPIs, and exports mostly use `TICKET_READ`; no approved reporting/KPI module code exists | Confirmed absence of a dedicated approved mapping; whether this is wrong is owner confirmation | `ReportingQueryService.cs`, `KpiService.cs`, `DashboardService.cs`, `ReportExportService.cs`; migration 031 has no REPORTING module | Owner approves reuse of `TICKETS/TICKET_READ` or a new catalogue. Only then update API checks, Angular routes, and grants together. |
| Attachment/evidence endpoints have no corresponding permission codes in migration 031 | Confirmed catalogue gap; ownership is unresolved | `AttachmentsController.cs`, `EvidenceController.cs`, `AttachmentService.cs`; migration 031 permission list | Owner chooses TICKETS sub-capabilities or separate `FILES`/`EVIDENCE` modules, then adds reviewed permissions and tests. No automatic grants. |
| Changes and problems are separate APIs and services but have no dedicated module/permission codes | Confirmed ownership gap; not proof of incorrect enforcement | `ChangesController.cs`, `ProblemsController.cs`, `ChangeService.cs`, `ProblemService.cs` | Owner decides whether they remain TICKETS feature types or become modules. Preserve current ticket scope until decision. |
| Communications and notifications have separate draft module codes and API families | No confirmed defect; boundary is supported by draft catalogue but UI/requirements are incomplete | migration 031 `COMMUNICATIONS` and `NOTIFICATIONS`; communication controllers | Keep separate provisionally. Owner confirms whether notification rules are delivery policy or communication content before UI/catalogue work. |
| Backend modules lack Angular administration screens | Confirmed UI coverage gap, not an API defect | `app.routes.ts`; shell future Reports link; admin catalogue/SLA/workflow/communication controllers | Add screens only for owner-approved modules; keep backend endpoints protected and document backend-only status meanwhile. |
| Legacy role compatibility branches remain | Confirmed compatibility behavior, not a defect | `RoleAuthorizationPolicy.cs`; RBAC design | Preserve IDs and branches. Require owner decision for retirement/mapping of `ADMINISTRATOR`, `OPERATIONS`, and `AUDITOR`; never silently remap. |

## Proposed catalogue for owner review

These are proposals, not approved grants. Scope is a separate authorization fact and must be evaluated for the same membership as the permission.

| Module | Feature/resource | Action | Proposed permission code | Scope requirement | Protected API operations | Angular use | Grant ceiling (subject to approval) | Status |
|---|---|---|---|---|---|---|---|---|
| TICKETS | Ticket | View/search | `TICKET_READ` (existing) | RECORD plus department/team/client; explicit organization scope only for organization-wide authority | GET `/api/v1/tickets*` | Home/list/detail | USER, ADMIN, ADMIN_HEAD, SUPER_ADMIN; SPECTATOR read-only | Existing and supported |
| TICKETS | Ticket lifecycle | Edit/create/transition | `TICKET_UPDATE` (existing) | RECORD and assigned/resource scope | POST `/api/v1/tickets*`, work-item and lifecycle routes | Create/detail actions | USER, ADMIN, ADMIN_HEAD, SUPER_ADMIN subject to role ceiling; never SPECTATOR | Existing and supported |
| USERS | User administration | Manage | `USER_ADMIN` (existing) | DEPARTMENT/client/resource administration scope | `/api/v1/admin/users*`, password reset | User administration | ADMIN, ADMIN_HEAD, SUPER_ADMIN subject to independent policy | Existing; persistence unverified |
| SECURITY | Role permission mapping | Configure | `ROLE_PERMISSION_ADMIN` (existing draft) | ORGANIZATION/security governance | `/api/v1/admin/roles*`, permissions, proposals | Role administration | Protected approval workflow; no SUPER_ADMIN self-expansion | Draft only |
| SECURITY | Role membership | Assign | `ROLE_ADMIN` (existing draft) | DEPARTMENT or approved membership scope | `/api/v1/admin/user-roles*` | Role administration | ADMIN/ADMIN_HEAD/SUPER_ADMIN only after owner-approved workflow | Draft only |
| SECURITY | Membership scope | Manage | `ROLE_SCOPE_ADMIN` (existing draft) | ORGANIZATION/security governance | `/api/v1/admin/role-scopes*` | No enabled grant UI | Independent approval required | Draft only |
| APPROVALS | Approval rules | View/manage/publish | `APPROVAL_READ`, `APPROVAL_ADMIN`, `APPROVAL_PUBLISH` (existing draft) | Ticket/department/organization as approved | `/api/v1/approvals*`, `/api/v1/approval-admin/*` | Future approval screen | ADMIN/ADMIN_HEAD/SUPER_ADMIN according to policy | Owner confirmation |
| AUDIT | Audit stream | View/export | `AUDITOR_VIEW`, `AUDIT_EXPORT` (existing draft) | RECORD; organization scope explicit | `/api/v1/auditor/reports*`, export routes | Future report/audit screen | Read-only roles for view; export ceiling owner-confirmed | Existing draft, UI absent |
| CONFIGURATION | Operational metadata | Manage | `CONFIG_ADMIN` (existing draft) | ORGANIZATION/security governance where protected | `/api/v1/admin/operational-metadata/*` | Future configuration screen | ADMIN/ADMIN_HEAD only if approved; SUPER_ADMIN subject to explicit scope | Owner confirmation |
| REPORTING | Reports/KPIs/dashboards | View/query | **No new code proposed until approved**; candidate is reuse of `TICKET_READ` | Ticket/client/department/team; organization scope explicit | `/api/v1/reports/*`, `/api/v1/dashboards/*`, `/api/v1/kpis/*` | Future report/dashboard screens | Read-capable roles according to approved catalogue | OWNER CONFIRMATION REQUIRED |
| REPORTING | Export | Export | **No new code proposed until approved**; candidate is `AUDIT_EXPORT` only for audit data, otherwise a distinct report export code | Same report resource scope | `/api/v1/reports/.../export` | Future export actions | Read/export roles according to approved catalogue | OWNER CONFIRMATION REQUIRED |
| FILES/EVIDENCE | Attachment | Upload/read/download | **No code proposed** | Ticket/resource scope | `/api/v1/attachments/*` | Ticket detail/file actions where implemented | TICKET_UPDATE for mutation and TICKET_READ for read only if owner confirms | OWNER CONFIRMATION REQUIRED |
| FILES/EVIDENCE | Evidence | Add/read | **No code proposed** | Ticket/resource scope | `/api/v1/evidence/*` | Ticket detail evidence actions | TICKET_UPDATE/TICKET_READ only if owner confirms | OWNER CONFIRMATION REQUIRED |
| TICKETS or CHANGES | Change lifecycle | Create/assess/schedule/execute | **No new code proposed** | Ticket/department/team/client | `/api/v1/changes/*` | No dedicated Angular screen | Existing ticket role ceilings until decision | OWNER CONFIRMATION REQUIRED |
| TICKETS or PROBLEMS | Problem lifecycle | Investigate/resolve/close | **No new code proposed** | Ticket/department/team/client | `/api/v1/problems/*` | No dedicated Angular screen | Existing ticket role ceilings until decision | OWNER CONFIRMATION REQUIRED |
| COMMUNICATIONS | Providers/templates | View/manage/publish/retire | `COMMUNICATION_READ`, `COMMUNICATION_ADMIN`, `COMMUNICATION_PUBLISH`, `COMMUNICATION_RETIRE` | Channel/provider and organization/delegated scope | `/api/v1/communication-admin/*`, `/api/v1/communications/*` | Future communication screen | ADMIN/ADMIN_HEAD/SUPER_ADMIN subject to approval | Draft supported |
| NOTIFICATIONS | Notification rules | View/manage/publish/retire | `NOTIFICATION_READ`, `NOTIFICATION_ADMIN`, `NOTIFICATION_PUBLISH`, `NOTIFICATION_RETIRE` | Delivery-policy/organization scope | Notification-rule routes under communication administration | Future notification screen | ADMIN/ADMIN_HEAD/SUPER_ADMIN subject to approval | Draft supported; boundary owner-confirmed |

## Module-boundary proposals

1. **Attachments versus evidence:** keep them separate as domain concepts. Attachments are arbitrary files and download/version operations; evidence is a ticket lifecycle record with evidentiary meaning. They may share storage and ticket scope, but a permission should not silently grant both until the owner approves that relationship.
2. **Changes/problems versus tickets:** retain separate services and route families. For authorization, treat them as ticket subtypes temporarily because both create and mutate ticket records and currently use ticket/resource scope. Dedicated module permissions require a product decision and a migration/catalogue change.
3. **Communications versus notifications:** keep separate. Communications represent channels, providers, templates, and sends; notifications represent event-triggered delivery rules. Their draft permission families support this boundary. Confirm whether a single UI owns both.
4. **Configuration metadata versus ticket configuration:** separate protected configuration from ticket operational metadata. `CONFIG_ADMIN` should govern security-sensitive configuration; ticket catalogue/priority/status metadata should retain its own approved catalogue permission. The current `TICKET_READ` calls need an owner classification before correction.
5. **Backend-only modules:** approval administration, catalogue, SLA, workflow, communication, notification, operational metadata, reports, and audit APIs currently lack complete Angular screens. Keep them backend-only and protected until product owners approve screens and journeys; do not interpret API existence as delivered UI functionality.

## Minimal correction plan

### Phase 1: owner decisions

Approve module boundaries, report/KPI/export permission strategy, file/evidence ownership, change/problem ownership, configuration classification, notification boundary, and legacy-role retirement policy. Resolve client `872002` ownership separately.

### Phase 2: catalogue contract

Record immutable module/feature/action/permission codes, security class, read-only flag, scope class, role ceilings, and approval requirements. Preserve all existing identifiers and legacy role IDs. No grants are added by this proposal.

### Phase 3: backend alignment

Update only the affected service authorization calls and controller/service tests after the catalogue contract is approved. Keep scope evaluation independent and fail-closed. Add provider-independent no-mutation denial tests for representative read/mutation routes.

### Phase 4: Angular alignment

Add permission guards, menus, screens, and action visibility only for approved modules. Each UI action must map to one API operation and one server permission. Add mocked-API tests for allowed, 401, 403, empty, loading, validation, and error states.

### Phase 5: migration planning

Prepare incremental PostgreSQL and Oracle metadata migrations only after catalog and role-ceiling approval. Reconcile existing permission codes, role IDs, memberships, and audit history. Execution remains subject to the established target, backup, baseline, and rollback gate.

## Acceptance criteria

- Every delivered UI action maps to an approved permission and API operation.
- Every business API operation has server-side permission and independent scope enforcement.
- No proposed permission is granted automatically.
- The five built-in roles and legacy identities remain unchanged.
- Provider-independent tests cover representative allowed/denied/no-mutation paths.
- Database/provider validation is separately approved and explicitly reported; source tests do not promote it.

Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

## Phase 1 validation outcome — operational metadata — 2026-10-16

Validation against `OperationalMetadataAdministrationController.cs`, `OperationalMetadataRepository.cs`, and migration 031 resolved the proposed Phase 1 issue without a code change. Ticket status/transition/priority metadata is explicitly mapped to the TICKETS module: reads use `TICKET_READ`, mutations use `TICKET_ADMIN`, and existing scope/role checks remain in force. `CONFIG_ADMIN` is a separate protected-configuration permission and is not established for these routes. The proposal’s operational-metadata item is therefore reclassified from confirmed mismatch to resolved audit interpretation. No other unresolved catalogue decision was implemented.

The applicable evidence remains the existing provider-independent tests and API build; no new test was required for an unchanged implementation. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.


## Reporting/KPI/export resolution boundary — 2026-10-16

The reporting review found no evidence that `TICKET_READ` is incorrectly broad for report queries, KPI calculations, dashboards, or export creation; `OPERATIONAL_REPORTS.md` and `KPI_ENGINE.md` explicitly require it. `AUDIT_EXPORT` remains audit-only. Export status/download and report-schedule permissions, revocation semantics, execution audit, sensitive-export approval, and Angular screens require owner decisions. No new permission names or grants are proposed as approved.
