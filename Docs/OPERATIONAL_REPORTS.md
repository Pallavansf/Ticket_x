# Phase 8.2 operational reports

All reports extend the Phase 8.1 read-only query layer and use `/api/v1/reports/...`. The common request contract keeps a default page size of 25 and a maximum of 100, allow-listed sorting, Asia/Kolkata date-only boundaries, and authorization before filtering, grouping, aggregation, or pagination.

## Catalogue

Implemented operational codes:

- `department-workload`, `team-workload`, `assignee-workload`
- `open-ticket-backlog`
- `ticket-type-summary`
- `ticket-aging-buckets`
- `tickets-waiting`
- `incident-summary`, `problem-summary`, `change-summary`
- `lifecycle`, `reopened-tickets`
- `sla-response-performance`, `sla-resolution-performance`, `sla-breached-tickets`
- `pending-approvals`, `approval-history`

The existing Phase 8.1 codes remain available: `ticket-volume`, `ticket-status-summary`, `ticket-aging`, and `assignment-workload`.

## Semantics

- Workload counts active, non-completed/non-cancelled work items and preserves department, team, and assignee identifiers.
- Backlog and type summaries use the authorized ticket population.
- Aging buckets use ticket `CreatedAt` and UTC elapsed age: 0–1, 2–3, 4–7, 8–14, 15–30, 31–60, 61–90, and 90+ days.
- Waiting reports use persisted `WaitingFor`, `WaitingStartedAt`, `WaitingMinutes`, and `WaitingResumedAt`; waiting reason values are not collapsed.
- Lifecycle and reopen reports use persisted event rows. Reopen output is limited to persisted event history.
- SLA reports consume persisted `SlaExecutionRow` state and never recalculate clocks, calendars, pauses, or breaches.
- Approval reports consume persisted approval and approval-history rows and are scoped through authorized ticket IDs.

## Security and performance

The existing `TICKET_READ` permission and administrator/department/team/client/assignee scope rules are applied before all report queries. EF Core queries are read-only, use `AsNoTracking`, and apply server-side filters, aggregates, ordering, and pagination. No raw SQL, export, mutation, migration, reporting database, ETL, or warehouse is introduced.

Report execution auditing remains deferred to the Auditor Reporting workstream because the existing audit chain is mutation-oriented.

## Known gaps and deferred scope

Some incident/problem/change relationship details and requester/owner display joins are not exposed by the current report DTOs. They are intentionally not inferred. KPI formulas, targets, thresholds, dashboards, Angular reporting, exports, dynamic report design, and the full auditor suite remain deferred. Oracle deployment is untouched.
