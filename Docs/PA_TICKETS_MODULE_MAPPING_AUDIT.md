# PA_TICKETS intended module mapping audit

Audit date: 2026-10-16. This is a source and documentation review only. No application behavior, schema, role identity, membership, permission grant, or database state was changed.

## Evidence boundary

The intended catalogue is recoverable only from the approved design narrative and unexecuted migration drafts. The most concrete catalogue is the `VALUES` list in `database/migrations/postgresql/031_configurable_rbac.sql` (and its Oracle counterpart); those files are design artifacts and do not prove deployment. The design explicitly says the earlier 030 scripts are superseded and incomplete. Actual server mappings are permission-code literals in application services and controller-level `[Authorize]`; actual UI mappings are `client/src/app/app.routes.ts`, `client/src/app/layout/shell.html`, and feature components. Database-backed role IDs, persisted permissions, and live grants are **UNVERIFIED**.

## Intended catalogue recovered from repository evidence

The draft catalogue defines these stable module codes: `APPROVALS`, `AUDIT`, `CATALOGUE`, `CLIENTS`, `COMMUNICATIONS`, `CONFIGURATION`, `NOTIFICATIONS`, `SECURITY`, `SLA`, `TICKETS`, `USERS`, and `WORKFLOWS`. Permission identifiers are the stable uppercase codes in migration 031 (including `APPROVAL_ADMIN`, `APPROVAL_PUBLISH`, `APPROVAL_READ`, `AUDIT_EXPORT`, `AUDITOR_VIEW`, `CATALOGUE_PUBLISH`, `CATEGORY_ADMIN`, `CATEGORY_READ`, `CLIENT_ADMIN`, `COMMUNICATION_*`, `CONFIG_ADMIN`, `NOTIFICATION_*`, `REQUEST_TYPE_*`, `ROLE_*`, `SERVICE_*`, `SLA_*`, `TICKET_*`, `USER_ADMIN`, and `WORKFLOW_*`). The intended scope classes are separate metadata (`NONE`, `RECORD`, `DEPARTMENT`, `ORGANIZATION`) and are evaluated against membership resource scopes. The five protected built-in role codes are exactly `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, and `SPECTATOR`.

Anything beyond this draft list, including a canonical feature taxonomy, report/KPI permission codes, attachment/file permission codes, or a complete role-to-module matrix, has no authoritative requirement evidence and is marked **OWNER CONFIRMATION REQUIRED**.

## Intended-versus-actual matrix

| Intended module / stable code | Actual module / code | Feature and action | Permission | Angular route/screen/action | API method and route | Server enforcement | Resource scope | Evidence | Status | Smallest correction / decision |
|---|---|---|---|---|---|---|---|---|---|---|
| TICKETS | Tickets / TICKETS | View/search ticket | TICKET_READ | `/app`, `/app/tickets`, `/app/tickets/:id`; permission guard | GET `/api/v1/tickets`, GET `/api/v1/tickets/{id}` | `TicketLifecycleService` and `DatabaseAuthorizationService` | Record plus department/team/client; explicit organization scope for organization-wide authority | 031; app.routes.ts; TicketsController.cs | MATCH | Preserve; verify persisted grant/scope later |
| TICKETS | Tickets / TICKETS | Create/update/lifecycle | TICKET_UPDATE | `/app/tickets/create`; detail mutation buttons | POST `/api/v1/tickets`; POST `/api/v1/tickets/{id}/*` | Ticket lifecycle authorizer, transition policy, spectator filter | Ticket/resource and assigned scope | 031; ticket pages; TicketLifecycleService.cs | MATCH | Preserve; provider E2E remains blocked |
| USERS | Administration / USERS | User administration/password reset | USER_ADMIN | `/app/admin/users`; user page actions | GET/PATCH/POST `/api/v1/admin/users*` and `/password` | AdministrationService permission and actor scope | Department/client/resource scope | 031; AdministrationController.cs; user-administration.page.ts | MATCH | Confirm exact owner-approved department semantics |
| SECURITY | Administration / SECURITY | Role and permission administration | ROLE_PERMISSION_ADMIN, ROLE_ADMIN, ROLE_SCOPE_ADMIN | `/app/admin/roles`; role editor and proposal actions | `/api/v1/admin/roles*`, `/permissions`, `/permission-proposals*`, `/user-roles*`, `/role-scopes*` | AdministrationService, RoleAuthorizationPolicy, SensitiveRbacApprovalPolicy | Organization/security governance for protected changes; membership scope separate | 031; role-administration.page.ts; AdministrationService.cs | PARTIAL / UNVERIFIED | Keep module grants separate from scopes; owner must approve independent scope/membership workflow |
| APPROVALS | Approval APIs / APPROVALS | Approval execution and decisions | APPROVAL_READ, APPROVAL_ADMIN, APPROVAL_PUBLISH | No Angular approval screen | POST `/api/v1/approvals*`; `/api/v1/approval-admin/*` | Approval repositories/services and explicit `APPROVAL_ADMIN` checks | Ticket/department and approver separation | 031; ApprovalsController.cs; ApprovalRuleAdministrationController.cs | UNVERIFIED | Owner confirmation required for UI and exact publish scope |
| AUDIT | Auditor reports / AUDIT | View/export audit records | AUDITOR_VIEW, AUDIT_EXPORT | Reports menu is a future placeholder; no screen | GET/POST `/api/v1/auditor/reports*`; report export routes | AuditorReportingService and ReportExportService | Record/ticket scope; organization requires explicit scope | 031; shell.html; AuditorReportingService.cs | PARTIAL | Confirm whether audit export is a separate delivered module and add UI only after approval |
| CATALOGUE | Catalogue administration / CATALOGUE | Services/categories/request types | SERVICE_READ/ADMIN, CATEGORY_READ/ADMIN, REQUEST_TYPE_READ/ADMIN, CATALOGUE_PUBLISH | No Angular catalogue screen | `/api/v1/admin/catalogue/*` | CatalogueAdministrationService | Department/organization according to metadata | 031; CatalogueAdministrationController.cs | UNVERIFIED | Owner confirmation required for feature taxonomy and UI ownership |
| CLIENTS | Administration / CLIENTS | Client membership/access | CLIENT_ADMIN | No Angular client-management route | POST/DELETE `/api/v1/admin/user-clients*` | AdministrationService | Client plus actor administration scope | 031; AdministrationController.cs | UNVERIFIED | Resolve client `872002` ownership before any mapping or grant |
| SLA | SLA administration / SLA | Policy/calendar administration and operations | SLA_READ/ADMIN/PUBLISH/RETIRE | No Angular SLA screen | `/api/v1/sla-admin/*`, `/api/v1/slas/*` | SLA repositories/services | Department/organization policy scope and ticket scope | 031; SlaAdministrationController.cs | UNVERIFIED | Owner confirmation required for module screen and permission granularity |
| WORKFLOWS | Workflow APIs / WORKFLOWS | Configure/validate/execute workflow | WORKFLOW_READ/ADMIN/PUBLISH | No Angular workflow screen | `/api/v1/workflow-admin/*`, `/api/v1/workflows/*`, `/api/v1/workflow-instances/*` | Workflow services; explicit `WORKFLOW_ADMIN` validation | Department/organization plus ticket scope | 031; workflow controllers | UNVERIFIED | Confirm publish versus execute ownership |
| COMMUNICATIONS | Communication APIs / COMMUNICATIONS | Providers, templates, notification rules, sends | COMMUNICATION_READ/ADMIN/PUBLISH/RETIRE and NOTIFICATION_* | No Angular communication screen | `/api/v1/communication-admin/*`, `/api/v1/communications/*` | Communication administration/service authorization | Channel/provider and recipient/resource scope | 031; communication controllers | UNVERIFIED | Owner confirmation required; do not collapse notification and communication modules |
| TICKETS | Operational metadata / TICKETS | Status, transition, and priority configuration | TICKET_READ for reads; TICKET_ADMIN for mutations | No Angular screen | `/api/v1/admin/operational-metadata/*` | OperationalMetadataRepository and authorization service | Department administration and existing ticket scope | 031; OperationalMetadataAdministrationController.cs | MATCH — audit interpretation corrected | Preserve current mapping; protected CONFIGURATION classification remains owner-confirmation only |
| REPORTING/KPI | Reports/dashboards/KPIs / no stable intended code | Reports, dashboards, KPI calculations | Actual code uses TICKET_READ for most report/KPI definitions and queries | No report screen; dashboard route is absent from current Angular routes | `/api/v1/reports/*`, `/api/v1/reports/.../export`, `/api/v1/dashboards/*`, `/api/v1/kpis/*` | ReportingQueryService, ReportExportService, DashboardService, KpiService | Ticket/client/department/team; explicit organization scope | Reporting services; app.routes.ts; inventory doc | MISMATCH / OWNER CONFIRMATION REQUIRED | Approve whether reporting is a TICKETS read capability or introduce separate report/KPI permissions; do not invent codes |
| ATTACHMENTS/EVIDENCE | Attachments/evidence / no draft permission code | File upload, download, evidence | No corresponding 031 permission identifier found; controller is authenticated and service-scoped | No Angular file-management route; ticket detail contains evidence actions where present | `/api/v1/attachments/*`, `/api/v1/evidence/*` | Attachment service and authenticated controller | Ticket/resource scope | 031 absence; AttachmentsController.cs; EvidenceController.cs | ORPHANED / OWNER CONFIRMATION REQUIRED | Requirements owner must define whether these are TICKETS capabilities or separate module permissions |
| CHANGES/PROBLEMS | Ticket submodules / no stable draft module code | Change/problem lifecycle | Uses ticket/service authorization; no dedicated CHANGE or PROBLEM permission in 031 | No dedicated Angular routes | `/api/v1/changes/*`, `/api/v1/problems/*` | ChangeService, ProblemService, ticket scope | Ticket/department/team/client | 031 absence; controllers/services | ORPHANED / OWNER CONFIRMATION REQUIRED | Confirm whether these are TICKETS features or separate modules before adding permissions |

This matrix is representative of the complete 232-route inventory. It intentionally does not convert every route into a new permission: the repository has no approved feature-level permission catalogue beyond migration 031.

## Role and scope consistency

- The source preserves the five required built-in codes. `RoleAuthorizationPolicy` enforces ceilings: SPECTATOR is read-only, USER allows read/business, ADMIN and ADMIN_HEAD allow department administration, and SUPER_ADMIN requires system-role metadata. Legacy `ADMINISTRATOR`, `OPERATIONS`, and `AUDITOR` compatibility branches remain in source and must not be silently remapped. Their ownership and retirement are **OWNER CONFIRMATION REQUIRED**.
- Permission metadata and membership scopes are separate in the application model. A permission's `ScopeClass` does not itself grant a department, team, client, record, or organization scope.
- The unexecuted 031 draft grants broad permission sets to roles and backfills scopes. Because it is not approved or applied, no role-to-permission or role-ID conclusion is a deployed fact.
- Organization-wide authorization is explicit and fail-closed. No account is granted it by source inspection.

## Representative journeys

1. **Ticket read:** Angular `/app/tickets` guard checks `TICKET_READ`; `GET /api/v1/tickets` is authenticated; `TicketLifecycleService` calls scoped authorization; effective role grant and membership resource scope are required. Angular and source tests cover the links; persisted grants are **UNVERIFIED**.
2. **Ticket mutation:** `/app/tickets/create` and detail actions check `TICKET_UPDATE`; POST lifecycle routes invoke the service and spectator filter. Denial/no-mutation is covered at service/policy level; provider persistence is **UNVERIFIED**.
3. **User administration:** `/app/admin/users` checks `USER_ADMIN`; administration routes enforce the same permission and scope. UI request tests exist; role/membership persistence is **UNVERIFIED**.
4. **Permission proposal:** `/app/admin/roles` uses `ROLE_PERMISSION_ADMIN`; proposal routes require server-derived initiator identity, independent approver checks, snapshot validation, and idempotency. UI/service tests exist; proposal tables are unexecuted.
5. **Report export:** no delivered Angular route; API export service rechecks `TICKET_READ` and scope. The missing report-specific permission is a mapping discrepancy requiring owner confirmation.
6. **File access:** attachment/evidence endpoints are authenticated and service-scoped, but no intended permission identifier or complete Angular journey is documented. Status is **ORPHANED / OWNER CONFIRMATION REQUIRED**.

## Prioritized correction plan (not implemented)

1. Obtain owner confirmation for reporting/KPI, attachment/evidence, change/problem, configuration metadata, and communication/notification module boundaries and stable permission identifiers.
2. Approve the exact role-to-permission ceiling matrix while preserving the five built-ins and all legacy IDs/references.
3. Decide whether `ROLE_ADMIN`, `ROLE_PERMISSION_ADMIN`, and `ROLE_SCOPE_ADMIN` are separate capabilities and define the independent approval path for scope and privileged membership changes.
4. Resolve client `872002` ownership before any client scope mapping.
5. Only after those decisions, update migration drafts and UI/API mappings; do not execute them until the existing migration evidence gate is satisfied.

Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**. This audit establishes source/documentation findings only; it does not establish database or provider compatibility.

## Correction proposal cross-reference — 2026-10-16

The discrepancy classifications and proposed catalogue are now in `Docs/PA_TICKETS_MODULE_MAPPING_CORRECTION_PROPOSAL.md`. The audit remains the evidence record; this proposal is not an approval to change permissions, roles, Angular routes, schemas, or migrations. Confirmed gaps are separated from owner decisions, and the five built-in roles plus legacy compatibility behavior remain preserved.

## Phase 1 resolution: operational metadata — 2026-10-16

The apparent configuration-metadata defect is **not confirmed as an implementation defect**. Source evidence establishes these endpoints as ticket operational configuration:

- `OperationalMetadataAdministrationController.cs` exposes status, transition, and priority definitions under `/api/v1/admin/operational-metadata`.
- `TicketStatusConfigurationRepository` and `TicketPriorityConfigurationRepository` use `TICKET_READ` for list/get/audit reads.
- Their create, update, activate, default, and transition mutations use `TICKET_ADMIN`.
- Unexecuted migration 031 explicitly defines `TICKET_ADMIN` as `TICKETS / CONFIGURE / DEPARTMENT / DEPARTMENT_ADMIN` with the description “Administer ticket status and priority.”
- `CONFIG_ADMIN` is defined separately as protected `CONFIGURATION / MANAGE / ORGANIZATION / SECURITY_GOVERNANCE`; no repository requirement assigns it to ticket status or priority metadata.

Therefore the smallest safe correction is **no behavior change**: retain `TICKET_READ` for reads and `TICKET_ADMIN` for mutations, with existing resource-scope and role-ceiling checks. The prior audit row suggesting a `CONFIG_ADMIN` mismatch is reclassified as **RESOLVED — audit interpretation corrected**. A future move of any endpoint into protected configuration remains an owner decision and would require an approved catalogue contract before code or migration changes.

No focused source test was added because no implementation change was justified. Existing provider-independent RBAC tests and the API build remain the applicable evidence; provider persistence and migration state remain unverified.


## Reporting/KPI/export review cross-reference — 2026-10-16

See `Docs/PA_TICKETS_REPORTING_AUTHORIZATION_MAPPING_REVIEW.md` for the 20-route inventory and current authorization contract. Existing requirements explicitly support `TICKET_READ` for report queries and KPI calculations, with ticket/client/department/team/assignee scope filtering. No approved reporting-specific permission exists. Export status/download and schedule mutation permission rechecks, execution auditing, sensitive-export approval, and Angular ownership remain owner decisions. No application behavior changed.
