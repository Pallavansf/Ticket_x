# PA Tickets API endpoint inventory

Static inventory completed 2026-10-16 from controller attributes, Program.cs, ApiSecurityConfiguration, PasswordRotationRequiredMiddleware, the spectator authorization filter, and application authorization services. No database or provider was contacted. The 26 controllers declare **232 HTTP routes**.

## Common outcome contract

Protected controller routes have class-level [Authorize] unless noted below. Anonymous requests are expected to receive **401**; authenticated requests missing permission, active membership, effective grant, or resource scope are expected to receive **403**; authorized requests reach the service and return their documented 2xx/4xx result. Password-rotation-required users receive 403 from the rotation middleware except for /api/v1/auth/change-password, login, and health/status paths. These are source-level expectations; hosted persistence and provider behavior remain unverified.

## Route-group inventory

Routes is the number of Http attributes in the controller. Every route in a group shares the authentication boundary; the application service performs the permission and resource-scope check shown below.

| Controller / route prefix | Routes | Auth | Permission/scope enforcement | Classification and coverage |
|---|---:|---|---|---|
| auth | 2 | Login public; change-password authenticated | Login has no permission; rotation endpoint is the only rotation exemption | Mutation; hosted 401/rotation tests; persistence unverified |
| system | 1 | Public | None (status only) | Read; intentional public endpoint |
| admin | 38 | Authenticated | Administration service checks user/role/permission/membership/client/scope; sensitive grants use proposal/approval policy | Mixed; unit and Angular role/user tests; persistence blocked |
| admin/catalogue | 25 | Authenticated | Catalogue permission plus department/team/client/resource scope | Mixed; service checks, no provider E2E |
| admin/operational-metadata | 15 | Authenticated | Operational metadata permission and resource scope | Mixed; service tests only |
| approval-admin | 13 | Authenticated | APPROVAL_ADMIN and approval-rule resource checks | Mixed; explicit permission test |
| approvals | 3 | Authenticated | Approval execution, initiator/approver separation, idempotency, ticket scope | Mutation; service tests, provider blocked |
| attachments | 4 | Authenticated | Attachment read/write and ticket scope | Read/mutation; service authorization tests |
| evidence | 2 | Authenticated | Evidence permission and ticket scope | Mutation/read; persistence blocked |
| auditor/reports | 5 | Authenticated | AUDITOR_VIEW and audit resource scope | Read; denied-permission test |
| changes | 14 | Authenticated | Change lifecycle and ticket/department/team/client scope | Mixed; service tests only |
| problems | 12 | Authenticated | Problem lifecycle and ticket/department/team/client scope | Mixed; service tests only |
| tickets | 18 | Authenticated | Ticket read/update, assignment, execution, approval, idempotency, resource scope | Mixed; focused lifecycle tests; provider blocked |
| work-items | 2 | Authenticated | Assignment/completion and ticket scope | Mutation; service tests only |
| workflow-admin | 13 | Authenticated | WORKFLOW_ADMIN plus workflow/ticket scope | Mixed; workflow authorization tests |
| workflow-instances | 2 | Authenticated | Workflow execution permission and ticket scope | Mutation; provider blocked |
| workflows | 1 | Authenticated | Explicit WORKFLOW_ADMIN check | Validation; controller permission path |
| sla-admin | 16 | Authenticated | SLA permission and department/team scope | Mixed; service tests only |
| slas | 1 | Authenticated | SLA operation permission and ticket scope | Mutation; provider blocked |
| communication-admin | 20 | Authenticated | Communication administration and channel/provider scope | Mixed; service tests only |
| communications | 5 | Authenticated | Send/read permission and recipient/resource scope | Mixed; service tests only |
| reports | 3 | Authenticated | Report query permission and ticket/client/resource scope | Read; report authorization tests |
| report-exports (api/v1/reports/.../export) | 3 | Authenticated | Export service re-checks report/ticket permission and scope | Export; denied export/query tests |
| report-schedules | 8 | Authenticated | Schedule permission and report scope | Mixed; schedule tests |
| dashboards | 3 | Authenticated | Dashboard/KPI permission and department/team/client scope | Read; dashboard/client-scope tests |
| kpis | 3 | Authenticated | KPI permission and effective client/department/team scope | Read; KPI scope tests |

The exact method and route template for every row is the checked-in Http attribute in the named controller; the count was recomputed directly from those attributes for this review.

## Findings

- Confirmed: all business controllers carry server-side authorization; Angular navigation is not a security boundary. ChangesController and ProblemsController explicitly carry Authorize.
- Confirmed: the spectator mutation filter denies unsafe verbs for SPECTATOR, while service checks enforce permission and scope for reads.
- Confirmed: effective grants are fail-closed (Active, ACTIVE status, known valid-from, and valid time window). Organization-wide access requires the explicit ORGANIZATION scope and is not automatically granted.
- Confirmed: login is rate-limited and anonymous; password change requires authentication and is the rotation exemption.
- Unresolved: independent approval and persistence of organization scope, historical audit actors, client 872002, and migration baseline prevent hosted authorization evidence.
- Unverified: SQL translation/materialization, provider parity, persisted role IDs, and cross-user isolation require an approved database target.

There is no provider-independent 232-route HTTP matrix. Existing focused tests cover middleware, role policy, effective-grant predicate, scopes, report export/query denial, audit permission, login/rotation, and Angular request flows. The remaining high-risk gap is a representative hosted, providerless HTTP matrix for 401/403 and no-mutation behavior; it must use isolated fakes and cannot establish SQL or provider behavior.
