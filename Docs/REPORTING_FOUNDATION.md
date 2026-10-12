# Phase 8.1 reporting foundation

## Architecture

Reporting is a read-only query layer over the existing operational PostgreSQL model:

`PA.Tickets.Api -> IReportingQueryService -> ReportingQueryService -> TicketsDbContext`

No reporting database, ETL, warehouse, copied ticket source, arbitrary SQL, or dynamic report designer is introduced. Report definitions and response contracts live in Application; provider-neutral EF Core LINQ lives in Infrastructure.

## Catalogue and endpoints

- `GET /api/v1/reports` lists the controlled catalogue.
- `GET /api/v1/reports/{reportCode}` returns one known definition.
- `POST /api/v1/reports/{reportCode}/query` runs one known report.

Canonical codes are `ticket-volume`, `ticket-status-summary`, `ticket-aging`, and `assignment-workload`. Reports use the existing `TICKET_READ` permission and the same department, team, client, and assignee scope rules as ticket access.

## Request and response

Requests support typed date, catalogue, identity, ticket type, priority, status, requester, assignee, sorting, page, and page size fields. Unsupported SLA filters are rejected explicitly. Responses contain report code/name, UTC generation time, applied filters, page metadata, total count, and DTO rows.

Dates are interpreted as calendar dates in `Asia/Kolkata`. A start date becomes local midnight inclusive; an end date becomes the next local midnight exclusive. Stored UTC timestamps are compared using those UTC boundaries and returned as UTC timestamps. This avoids ambiguous timestamp `BETWEEN` behavior.

The default page size is 25 and the hard maximum is 100. Sorting is an allow-list per report and always has a deterministic secondary key. Invalid report codes, filters, sorts, and page values return controlled 4xx responses.

## Security, audit, and performance

Every query requires JWT authentication and `TICKET_READ`. Non-administrator results are scoped before grouping or pagination so inaccessible tickets do not affect totals or aggregates. Queries use `AsNoTracking`, server-side filtering, grouping, ordering, count, skip, and take. Report execution itself is not written to the existing operational audit chain in this slice; the decision is documented here because that chain is designed for entity mutations and adding report events would create a second audit event contract.

## Boundaries

This phase does not implement KPI formulas, targets, dashboards, Angular reporting UI, exports, dynamic SQL, a warehouse, ETL, Oracle deployment, or Phase 8 production operations.