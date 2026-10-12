# PA Tickets Phase 9.2 — Authenticated shell and first API integration

## Selected screen

The first API-backed screen is the read-only **Tickets** search screen. It was selected because the frozen API map and `TicketsController` expose a complete GET contract with server-side filters and pagination, while no Angular business screen existed yet.

### Backend contract used

- Method: `GET`
- Endpoint: `/api/v1/tickets`
- Controller: `TicketsController.Search`
- Response: `TicketSearchPage { items: TicketView[], page, pageSize, totalCount }`
- Supported query values: `number`, numeric `status`, numeric `type`, `priorityId`, `serviceId`, `categoryId`, `requestTypeId`, `clientId`, `departmentId`, `teamId`, `assigneeId`, `createdFrom`, `createdTo`, `page`, `pageSize`.
- This UI uses number, status, type, created-from, created-to, page, and pageSize. Sorting is not offered because the backend contract does not expose sorting.
- The endpoint is authenticated and performs server-side actor, permission, and scope checks. No client permission name was invented.

## Implemented behavior

- API access is centralized in `TicketsApiService` and uses the existing environment API base URL and interceptor.
- The shell exposes only Home and the implemented Tickets screen. Reports and other future areas remain identified as later-phase items.
- Filters are keyboard-accessible and submit server query values. Date boundaries are sent as UTC ISO start-of-day and end-of-day values.
- Pagination uses the API’s page and page-size response values; no client-side business rules or fake totals are added.
- Loading, empty, authentication-required (401), forbidden (403), server, and network failure states are rendered explicitly. Failed requests are not retried automatically.
- No fixture data is rendered when the API fails.

## Authentication boundary

The backend validates externally issued bearer/OIDC tokens and does not expose `/login`, `/token`, or `/refresh`. The existing Angular service keeps tokens in memory only. The interceptor supplies the approved token handoff when one exists; a 401 clears the session and routes to login. Guards are navigation UX and do not replace backend authorization. No identity provider, token, bypass, or production credential was added.

A valid approved token is still required to exercise the protected endpoint. Until the identity provider contract is supplied, the running screen displays the authentication-required state rather than claiming authenticated integration.

## Tests and verification

Client commands executed from `client/`:

```text
npm test -- --no-watch --no-progress
npm run build
```

Results:

- Angular tests: **6 files passed, 14 tests passed, 0 failed, 0 skipped**.
- Production build: **passed, 0 warnings, 0 errors**.
- Lint: no lint script is configured.
- Type checking: completed by the successful Angular production build.

Coverage includes service URL/query serialization, successful fixture rendering, empty state, loading state, 401/403/server/network mappings, filter and pagination requests, shell auth behavior, and no fabricated rows on empty/failure responses.

Existing backend regression command:

```text
dotnet test .\tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore
```

Result: **162 passed, 0 failed, 0 skipped**.

## Manual verification

1. Start the API on the configured development URL (`http://localhost:5018`) and the Angular client with `npm start`.
2. Obtain a bearer token only through the approved identity-provider integration when available; hand it to the existing host integration through `AuthService.setBearerToken()`.
3. Open `/app/tickets`, confirm the request is `GET /api/v1/tickets` with `Authorization` and `X-Correlation-ID`, apply filters, and move between pages.
4. Verify empty, 401, 403, server, and offline states with controlled responses. Do not use production credentials in tests.

## Gate status and limitations

- Application shell: **PASS**
- First API-backed screen implementation: **PASS**
- Error and authorization handling: **PASS** (automated controlled-response tests)
- Automated tests: **PASS — 14/14**
- Production build: **PASS**
- Protected API integration smoke test: **BLOCKED** — no valid approved bearer token/OIDC handoff is configured, so no authenticated live request is claimed.
- OIDC end-to-end authentication: **BLOCKED** — requires approved provider, client registration, redirect URI, scopes/audience, token acquisition and handoff, logout behavior, and backend issuer/audience validation alignment.

No Oracle connection, backend schema change, migration, or authentication architecture change was made.
