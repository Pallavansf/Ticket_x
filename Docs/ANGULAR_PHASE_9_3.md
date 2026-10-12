# PA Tickets Phase 9.3 — Ticket details and work-item views

## Contract-first findings

The inspected API contracts provide:

- `GET /api/v1/tickets/{id:long}` (`TicketsController.Get`) returning `TicketView`.
- `TicketView`: `Id`, `Number`, `Type`, `Status`, `Subject`, `PriorityId`, `CurrentDepartmentId`, `CurrentTeamId`, `CurrentAssignee`, `WorkItems`, `ServiceId`, `CategoryId`, `SubCategoryId`, `RequestTypeId`, and `RequestTypeVersion`.
- `WorkItemView` inside `TicketView`: `Id`, `Number`, `Status`, `AssignedToUserId`, `WaitingFor`, `WaitingComment`, `WaitingEvidenceReference`, `WaitingExpectedDate`, and `WaitingMinutes`.
- `WorkItemsController` has only POST mutation routes (`complete` and `assign`); no authorized read endpoint exists.
- `WorkflowExecutionController` and `ApprovalsController` expose only POST mutation routes; no ticket detail read endpoints exist for workflow or approval history.

The detail view therefore uses the ticket GET response and the embedded authorized work-item collection only. It does not invent requester/date fields, workflow history, approval details, assignment actions, execution events, or separate work-item URLs.

## Implemented views

- Ticket numbers in the list navigate to `/app/tickets/{id}` using the backend’s documented numeric route identifier.
- Detail requests use `GET /api/v1/tickets/{id}` through the existing API service and interceptor.
- Displayed fields are limited to the returned DTO: number, subject, type, status, priority, department, team, assignee, service, category, request type, ticket ID, and embedded work-item fields.
- Work items are read-only and show number, status, assigned user, and waiting minutes.
- Detail includes an accessible route back to `/app/tickets`.
- Explicit loading, unauthorized, forbidden, not-found, server, network, and empty work-item states are provided.
- No bearer token persistence, backend changes, Oracle access, or mutation UI was added.

## Verification

Commands executed from `client/`:

```text
npm test -- --no-watch --no-progress
npm run build
```

Results:

- Angular tests: **8 files passed, 17 tests passed, 0 failed, 0 skipped**.
- Production build: **passed, 0 warnings, 0 errors**.
- No lint script is configured. Type checking completed through the successful production build.

Tests cover list-to-detail URL construction, detail API URL and numeric identifier, successful DTO/work-item rendering, not-found, 401/403, server/network failures, back navigation, and no fabricated data on failed responses.

No backend changes were approved or made, so backend regression tests were not rerun in this frontend-only phase. The prior Phase 9.2 backend regression result remains 162 passed, 0 failed, 0 skipped.

## Manual verification

1. Start the API at the configured `http://localhost:5018` URL and start Angular with `npm start`.
2. Supply a valid bearer token only through the approved external identity-provider handoff.
3. Open `/app/tickets`, select a ticket number, and confirm the browser requests `/api/v1/tickets/{id}` with the existing bearer and correlation-ID headers.
4. Confirm the detail page shows only returned fields and embedded work items, and that Back to ticket search returns to `/app/tickets`.
5. Exercise controlled 401, 403, 404, server, network, and empty-work-item responses.

## Gate status and limitations

- Contract inspection: **PASS**
- Ticket detail implementation: **PASS**
- Read-only work-item display: **PASS**, embedded collection only
- Workflow/approval read view: **NOT IMPLEMENTED — no GET contract exists**
- Automated tests: **PASS — 17/17**
- Production build: **PASS**
- Live protected API smoke test: **BLOCKED** pending an approved valid bearer token/OIDC handoff
- OIDC end-to-end authentication: **BLOCKED**

The phase is implementation-complete with live authenticated verification explicitly blocked by the existing OIDC dependency.
