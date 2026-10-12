# Dashboard API Foundation (Phase 8.4)

Dashboard APIs are a controlled composition layer over the existing KPI and operational report services. They contain no SQL, KPI formulas, workflow rules, snapshots, persistence, caching, or dynamic widget designer.

## Catalogue

The controlled dashboards are `operations`, `sla`, and `workload`. Definitions are application code, version 1, enabled, and require `TICKET_READ`. Supported periods are day, week, month, and custom. Filters reuse the existing scope dimensions: client, department, team, service, category, subcategory, request type, ticket type, priority, requester, and assignee.

## Endpoints

- `GET /api/v1/dashboards`
- `GET /api/v1/dashboards/{dashboardCode}`
- `POST /api/v1/dashboards/{dashboardCode}/query`

Periods use the existing KPI/reporting semantics: Asia/Kolkata, start inclusive, end exclusive. Summary widgets are bounded; table data is returned through the existing paginated report result.

## Reuse and authorization

KPI widgets call `IKpiService` and return its result directly. Operational widgets call `IReportingQueryService`; dashboard code only wraps the report result as widget data. Authorization is checked before composition and both dependencies apply their own authorized-population filtering before aggregation. Dependency exceptions are allowed to surface through the existing controlled API error handler and are never converted to zero.

Widget types are controlled: KPI, trend, distribution, table, workload, and aging. Widget definitions and source codes cannot be submitted by callers.

Historical KPI trends are not fabricated because KPI snapshots are not persisted. Angular UI, exports, auditor reporting, caching, ETL, database persistence, and Oracle execution are deferred.
