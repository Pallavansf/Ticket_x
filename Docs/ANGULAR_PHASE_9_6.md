# PA Tickets Phase 9.6 — UI hardening and end-to-end readiness

## Audit scope and findings

Audited the Phase 9.1–9.5 Angular routes, shell, list, detail, creation form, completion action, services, guards, interceptor, styles, tests, API contracts, and the backend work-item execution repository.

| Severity | Finding | Evidence | Result |
|---|---|---|---|
| High | Expiry was evaluated by a cached signal and could remain authenticated after the token expired. | `AuthService.isAuthenticated` was a computed value dependent only on the session signal; clock time does not invalidate it. | Fixed with `isSessionValid()` and used it in guards/interceptor. |
| High | A confirmed completion followed by a failed detail refresh was reported through the mutation error state. | `TicketDetailPage.refreshTicket()` called `handleActionError()` for refresh failures. | Fixed with separate `refreshError` state and message. |
| Medium | The Phase 9.4 documentation file was missing even though the implementation existed. | Documentation audit found 9.1, 9.2, 9.3, and 9.5 but no 9.4 file. | Restored a concise Phase 9.4 contract record. |
| Low | Completion confirmation had no explicit regression coverage for refresh failure distinction. | Existing detail tests covered completion and conflicts but not post-success refresh failure. | Added regression test. |

No fabricated endpoints, permissions, data, token persistence, backend changes, database changes, or Oracle access were introduced.

## Hardening changes

- Expired in-memory sessions are rejected using the current clock before guard authorization or bearer-header attachment.
- Work-item completion remains a single non-retried POST. After a successful completion response, a detail refresh failure is reported as “completion confirmed; refresh failed,” preserving the mutation result and avoiding a false failure message.
- Existing 401, 403, 404, 409, server, network, loading, empty, and validation states remain explicit.
- Existing API base URL, `/api/v1` routing, correlation-ID interceptor, in-memory token boundary, and same-origin return URL validation remain unchanged.
- Existing reactive forms, confirmation controls, keyboard buttons, labels, table headings, responsive overflow, visible focus styles, and mobile layout were retained. Action confirmation uses inline keyboard-focusable controls with explicit cancel behavior.

## Idempotency verification

The backend was inspected rather than assumed. `WorkItemsController` requires `Idempotency-Key`, and `DurableWorkflowApprovalRepositories.CompleteWorkItemInternalOnceAsync` checks scope `WORKFLOW_STEP_COMPLETE`, compares the request fingerprint, returns `Already processed` for a repeated key, and persists the key in the same serializable transaction as the completion. Reusing a key for a different work item is rejected. The Angular client generates one key per user attempt and does not retry ambiguous POST failures.

## Verification

Commands executed from `client/`:

```text
npm test -- --no-watch --no-progress
npm run build
```

Results:

- Angular tests: **10 files passed, 25 tests passed, 0 failed, 0 skipped**.
- Production build: **passed, 0 warnings, 0 errors**.
- Lint: no lint script is configured.
- Type checking: passed through the Angular production build.

The suite covers auth expiry, interceptor behavior, list/detail/create flows, completion confirmation, idempotency header, refresh after mutation, refresh-failure separation, validation, 401/403/404/409/server/network states, and no fabricated success/data.

No backend change was necessary or made, so backend tests were not rerun in this frontend-only hardening phase. The prior verified backend regression remains 162 passed, 0 failed, 0 skipped.

## Remaining risks and limitations

- Approved OIDC provider, client registration, scopes/audience, redirect URI, token handoff, and logout behavior remain unavailable. Protected live API verification is **BLOCKED**.
- UI guards and hidden actions are not security boundaries; backend authorization remains authoritative.
- Dynamic request fields remain blocked by missing field-definition/options read contracts.
- No assignment, ticket lifecycle, workflow, or approval mutation UI was added.
- No lint command exists in the repository.

## Gate status

- Audit and verified defect fixes: **PASS**
- Accessibility/responsive hardening: **PASS** by code inspection and regression tests
- Automated tests: **PASS — 25/25**
- Production build: **PASS**
- Protected live API/OIDC readiness: **BLOCKED**
- Oracle: **not touched**

UI hardening is complete. End-to-end authentication remains separately blocked until the approved OIDC integration is configured and exercised.
