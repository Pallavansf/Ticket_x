# PA Tickets Phase 9.5 — Ticket operations and work-item actions

## Contract inventory

Inspected controller contracts:

- `POST /api/v1/work-items/{publicId}/complete` (`WorkItemsController.Complete`): no body, required `Idempotency-Key`, response `DurableMutationResult`. The server resolves the actor from the bearer subject and enforces authorization and current work-item state.
- `POST /api/v1/work-items/{publicId}/assign` (`WorkItemsController.Assign`): `WorkItemAssignmentRequest { DepartmentId?, TeamId?, UserId?, Reason? }`, required `Idempotency-Key`, response `DurableMutationResult`. This remains unsupported in the UI because assignment selection and authorization context are not available in the current screen.
- Ticket lifecycle POST routes exist for classify, route, approval, approve, execute, wait, resume, evidence, complete, resolve, reopen, cancel, close, and relationships. They are not exposed here because this phase has no approved UI contracts for their required actor/target fields, preconditions, or workflow/approval context beyond the server routes.
- Workflow execution and approval controllers expose mutation routes only; no additional read state was invented.

The API contract map identifies 401, 403, 404, 409, validation, server, and correlation-ID behavior. No client permission name or transition rule was fabricated.

## Selected action

The implemented action is **complete work item** from the existing ticket detail page. `WorkItemView.Number` is the public identifier available in the authorized ticket response and is passed as `{publicId}`. The UI asks for confirmation, generates one idempotency key per attempt, disables repeated clicks while pending, and refreshes `GET /api/v1/tickets/{ticketId}` only after the completion POST confirms success.

There is no optimistic success state. A network failure reports that the outcome is unknown and is not retried. A 409 is shown as a state-change/concurrency conflict. 401, 403, 404, and server failures have distinct messages.

## Verification

Commands from `client/`:

```text
npm test -- --no-watch --no-progress
npm run build
```

Results:

- Angular tests: **10 files passed, 23 tests passed, 0 failed, 0 skipped**.
- Production build: **passed, 0 warnings, 0 errors**.
- No lint script is configured; type checking passed through the production build.

Coverage includes action visibility, confirmation, idempotency header and route, duplicate-click prevention, successful refresh, 401/403/404/409/server/network handling, and no false success state.

## Manual verification

1. Start the API at `http://localhost:5018` and the Angular client with `npm start`.
2. Supply a valid bearer token only through the approved OIDC handoff.
3. Open `/app/tickets/{id}`, choose Complete on a returned work item, confirm, and verify `POST /api/v1/work-items/{publicId}/complete` includes `Idempotency-Key`.
4. Verify the detail GET refreshes after a confirmed response.
5. Exercise controlled 401, 403, 404, 409, server, and offline responses. Confirm no mutation retry occurs after an ambiguous network failure.

## Gate status and limitations

- Contract inspection: **PASS**
- Implemented operational action: **PASS — work-item completion**
- Authorization and error handling: **PASS** through controlled-response tests; backend remains authoritative
- Automated tests: **PASS — 23/23**
- Production build: **PASS**
- Live authenticated integration: **BLOCKED** pending approved bearer/OIDC handoff
- Unsupported actions: assignment, ticket lifecycle transitions, workflow, and approval actions remain unimplemented in this read-focused UI because their complete client contracts/context were not established for this phase.
- Oracle and backend architecture: unchanged

Phase 9.5 is implementation-complete with live authenticated operation explicitly unverified.
