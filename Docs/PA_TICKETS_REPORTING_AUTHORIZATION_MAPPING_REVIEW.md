# PA_TICKETS reporting, KPI, dashboard, schedule, and export authorization review

Review date: 2026-10-16. Static source/documentation inspection only. No database/provider access or behavior changes occurred.

## Current API inventory

| Operation family | HTTP method and route | Service | Current permission | Scope filtering | Audit behavior | Status |
|---|---|---|---|---|---|---|
| Report catalogue | GET `/api/v1/reports` | `ReportingQueryService.Catalogue` | Controller `[Authorize]`; no service permission | None; definitions only | None | Authenticated metadata read |
| Report definition | GET `/api/v1/reports/{reportCode}` | `ReportingQueryService.GetDefinition` | Controller `[Authorize]`; no service permission | None; definitions only | None | Authenticated metadata read |
| Report query | POST `/api/v1/reports/{reportCode}/query` | `ReportingQueryService.QueryAsync` | Definition permission: currently `TICKET_READ` | Authorized tickets filtered by assignee, department, team, client; Super Admin organization access requires explicit organization scope | `Docs/OPERATIONAL_REPORTS.md` says execution audit deferred | Current contract supported by requirements |
| KPI catalogue | GET `/api/v1/kpis` | `KpiService.Catalogue` | Controller `[Authorize]`; no service permission | None; definitions only | None | Authenticated metadata read |
| KPI definition | GET `/api/v1/kpis/{kpiCode}` | `KpiService.GetDefinition` | Controller `[Authorize]`; no service permission | None; definitions only | None | Authenticated metadata read |
| KPI calculation | POST `/api/v1/kpis/{kpiCode}/calculate` | `KpiService.CalculateAsync` | Explicit `TICKET_READ` | Client, department, team, service, category, subcategory, request type, ticket type, priority, requester, assignee; organization scope requires explicit scope | `Docs/KPI_ENGINE.md` says execution audit deferred | Current contract explicitly supported |
| Dashboard catalogue/definition | GET `/api/v1/dashboards`, GET `/api/v1/dashboards/{dashboardCode}` | `DashboardService` | Controller `[Authorize]`; query definitions use `TICKET_READ` | Query delegates to KPI/report services and their ticket scope | No independent dashboard audit | Current backend-only contract |
| Dashboard query | POST `/api/v1/dashboards/{dashboardCode}/query` | `DashboardService.QueryAsync` | Definition permission currently `TICKET_READ`; delegated report/KPI calls recheck | Same ticket/client/department/team scope | No independent execution audit | Current contract; UI absent |
| Export creation | POST `/api/v1/reports/{reportCode}/exports` | `ReportExportService.CreateAsync` | Rechecks report definition permission, currently `TICKET_READ` | Underlying report query rechecks authorized ticket scope | Creates execution row/storage artifact; no audit-chain event | Export permission is not distinct in source |
| Export status | GET `/api/v1/exports/{exportId}` | `ReportExportService.GetAsync` | Ownership (`RequestedBy == actor`) only; no current report permission recheck | Ownership only | Expiry may mutate execution row; no audit-chain event | Authorization contract requires owner decision |
| Export download | GET `/api/v1/exports/{exportId}/download` | `ReportExportService.DownloadAsync` | Ownership only; no current report/export permission recheck | Ownership only | Expiry/deletion may mutate storage; no audit-chain event | Sensitive export decision required |
| Schedule list/get/executions | GET `/api/v1/report-schedules`, GET `/{id}`, GET `/{id}/executions` | `ReportScheduleService` | Controller auth plus owner row filter; no permission on reads | Owner-only schedule scope; underlying report query later applies ticket scope | Worker/execution records; no audit-chain event | Schedule permission not defined |
| Schedule create | POST `/api/v1/report-schedules` | `ReportScheduleService.CreateAsync` | Report definition permission, currently `TICKET_READ` | Filters are applied when report executes | Persists schedule; no audit-chain event | Schedule permission not defined |
| Schedule update/status | PUT/POST `/api/v1/report-schedules/{id}/*` | `ReportScheduleService` | Owner row filter; no current permission recheck | Owner-only schedule scope | Persists changes; no audit-chain event | Schedule permission not defined |

There are 20 routes in these families: 3 reports, 3 KPIs, 3 dashboards, 3 exports, and 8 schedules. All controllers require authentication. No Angular route currently delivers reports, dashboards, KPI, export, or schedule screens; the shell only shows a future Reports placeholder when `AUDIT_EXPORT` or `AUDITOR_VIEW` is present. The login test's `/app/reports` return URL is not a delivered route.

## Intended evidence and conclusion

`Docs/OPERATIONAL_REPORTS.md` explicitly establishes `TICKET_READ` before report filtering, grouping, aggregation, and pagination. `Docs/KPI_ENGINE.md` explicitly states every calculation requires `TICKET_READ` and documents the supported scope dimensions. Migration 031 contains `AUDIT_EXPORT` and `AUDITOR_VIEW` for audit records, but no reporting, KPI, dashboard, export, or schedule permission. Therefore no repository evidence authorizes replacing report/KPI permissions with a new name or treating `AUDIT_EXPORT` as a general report-export permission.

The current use of `TICKET_READ` for report queries, KPI calculations, dashboards, and export creation is **supported by existing requirements**. It is not an over-broad mapping proven incorrect by this review. Export status/download and schedule mutation paths have a separate contract gap: they rely on ownership after an authenticated request and do not recheck a current report/read/export permission. Whether ownership is the intended artifact boundary or a current-permission recheck is required is **OWNER CONFIRMATION REQUIRED**.

Report and KPI definitions are application-controlled and read-only. No administrative definition mutation endpoint exists. Schedule creation/update/status is an operational mutation, but no approved schedule-management permission exists. Do not invent one.

## Role and scope implications

- SPECTATOR can only receive read-only permissions under `RoleAuthorizationPolicy`; the current `TICKET_READ` mapping is compatible with read-only report/KPI access, subject to the same ticket scope.
- USER may receive read/business permissions; ADMIN and ADMIN_HEAD may receive department-admin permissions; SUPER_ADMIN still requires explicit effective organization scope for organization-wide data. No grant is added by this review.
- Report filters never widen authorization: `AuthorizedTickets` and KPI scope filtering constrain the source population before aggregation. Dashboard delegation preserves these checks.
- Exports contain report data and therefore inherit the underlying report scope at creation. Download currently uses artifact ownership only; an owner decision is needed for revocation and independent export authorization.
- Existing documentation defers report/KPI execution auditing. Sensitive export audit requirements are not established by an approved contract.

## Proposed contract for owner approval

1. Keep `TICKET_READ` for operational report queries, KPI calculations, dashboard queries, and export creation until an approved reporting catalogue says otherwise.
2. Do not reuse `AUDIT_EXPORT` for operational reports; its documented module is `AUDIT` and its purpose is audit-record export.
3. Decide whether export status/download must recheck the current underlying read permission, require a distinct approved export permission, or remain owner-only after creation. Define revocation, retention, expiry, and audit event semantics.
4. Decide whether schedules require a distinct approved management permission or remain owner-owned operations governed by the underlying report permission. Define whether update/status operations recheck permission after creation.
5. Decide whether report/KPI/dashboard definition reads should remain authenticated metadata reads or require the same read permission. No current requirement chooses one.
6. Define independent audit events and approval requirements for sensitive operational exports before enabling any new permission or grant.
7. Approve Angular ownership and journeys for reports, dashboards, KPI, exports, and schedules. Until then they remain backend-only.

## Required regression tests before implementation

- Allowed and denied report queries with department/team/client/assignee scope; verify no rows outside the authorized population.
- Allowed and denied KPI and dashboard calculations with the same scope boundaries.
- SPECTATOR read access and mutation denial for any future schedule/export UI.
- Export creation denial without `TICKET_READ`, row-scope preservation, owner isolation, expiry, and behavior after permission revocation (after the contract is approved).
- Export download/status authorization for owner and non-owner, including any approved recheck semantics.
- Schedule create/update/pause/resume/cancel authorization, owner isolation, and permission revocation behavior.
- Catalogue/definition endpoint decision tests if a permission requirement is approved.
- Audit-event and independent-approval tests for any sensitive export operation.

All tests must remain provider-independent until a separately approved database/provider phase. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

### Final export lifecycle decision proposal — 2026-10-16

Added `Docs/PA_TICKETS_EXPORT_LIFECYCLE_AUTHORIZATION_DECISIONS.md`. The recommended review baseline is current requester identity, active account, current report permission, and current applicable scope for status/download; non-owner requests remain non-disclosing; expiry blocks access while cleanup is separate; audit events cover creation, failure, download, denial, expiry, and deletion. Seven-day retention, artifact authorization context, distinct export permission, and sensitive-export approval remain explicit owner decisions. No behavior changed.

### Export lifecycle implementation update — 2026-10-16

The export service now rechecks the current report permission for owner status/download reads, reports completed artifacts as expired without GET-triggered writes/deletion, and fails closed on download when the existing artifact row lacks a row-level authorization snapshot. Report generation is unchanged. Audit event persistence and asynchronous physical cleanup remain blocked pending approved transaction/worker contracts and provider validation. Focused provider-independent export tests passed 5/5; API build passed 0 warnings/0 errors. No database access occurred.
