# KPI Engine (Phase 8.3)

The KPI engine calculates controlled measurements on demand from persisted operational state. It does not execute caller supplied SQL, expressions, scripts, or formulas, and it does not persist a second copy of ticket data.

## Periods

`Day`, `Week`, `Month`, and `Custom` are supported. Boundaries are explicit, use Asia/Kolkata, and are start inclusive/end exclusive (`start <= eventAt < end`). Custom periods require `From < To`.

## Catalogue

The eight controlled definitions are `SLA_COMPLIANCE`, `FIRST_RESPONSE_TIME`, `RESOLUTION_TIME`, `OPEN_BACKLOG`, `AVERAGE_TICKET_AGE`, `REOPEN_RATE`, `APPROVAL_TURNAROUND`, and `WAITING_TIME`. Definitions are application code, version 1 effective 2026-01-01. No administrator mutation endpoint exists; changing a definition requires a new application version.

SLA compliance uses completed persisted SLA rows and `BreachedAt`; first response uses only persisted `FIRST_RESPONSE` or `RESPONSE_RECEIVED` lifecycle events; resolution uses `ResolvedAt`; backlog and age use configured open statuses; reopen rate uses persisted lifecycle events containing `REOPEN`; approval turnaround uses approval instance `CreatedAt` and `CompletedAt`; waiting time uses persisted work item `WaitingMinutes` and `WaitingStartedAt`.

For every ratio, a zero denominator returns `Value = null` and `NOT_APPLICABLE`. Duration is minutes, percentages are multiplied by 100 and rounded to two decimals, and counts are integers. Threshold status is separate from value; absent thresholds are `UNCONFIGURED`.

## Authorization and API

Every calculation requires `TICKET_READ`. The authorized ticket population is filtered before aggregation and supports client, department, team, service, category, subcategory, request type, ticket type, priority, requester, and assignee dimensions.

- `GET /api/v1/kpis`
- `GET /api/v1/kpis/{kpiCode}`
- `POST /api/v1/kpis/{kpiCode}/calculate`

KPI execution auditing is deferred to the Auditor Reporting workstream. Dashboards, exports, snapshots, caching, ETL, Oracle execution, and Angular UI are outside Phase 8.3. Oracle mappings are untouched.
