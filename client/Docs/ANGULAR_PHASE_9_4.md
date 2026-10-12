# PA Tickets Phase 9.4 — Ticket creation and catalogue-driven forms

## Contract inspection

The inspected contracts provide:

- `POST /api/v1/tickets` (`TicketsController.Create`) with `CreateTicketRequest`: `Type`, `Subject`, `Description`, optional `RequestTypeId`, optional `FieldValues`, optional `ServiceId`, `CategoryId`, and `SubCategoryId`. The response is `TicketView`.
- `Idempotency-Key` is an actual required creation header contract and is generated once per submit attempt. POST failures are not automatically retried.
- `GET /api/v1/admin/catalogue/services` returns `CatalogueService[]`.
- `GET /api/v1/admin/catalogue/categories?serviceId=` returns `CatalogueCategory[]`.
- `GET /api/v1/admin/catalogue/sub-categories?serviceId=&categoryId=` returns `CatalogueSubCategory[]`.
- `GET /api/v1/admin/catalogue/request-types?serviceId=&categoryId=&subCategoryId=` returns `CatalogueRequestType[]`.
- `GET /api/v1/admin/catalogue/request-types/{id}/fields` returns only `CatalogueRequestField[]` associations. The backend does not expose a read endpoint for field definitions or options, so dynamic request fields cannot be rendered safely in this phase.
- No client lookup endpoint is required by `CreateTicketRequest`; no fabricated client selector was added.
- Ticket types use the actual domain enum values: Request 1, Incident 2, Problem 3, Task 4, Change 5.

## Implemented experience

- Added a Create ticket action to the existing Tickets screen and route `/app/tickets/create`.
- Added a responsive reactive form for the documented DTO fields: ticket type, subject, description, service, category, subcategory, and request type.
- Service, category, subcategory, and request type options come only from authorized API responses. Dependent selections clear downstream values and request supported filtered endpoints.
- Inactive catalogue entries are excluded using the returned `Active` flag; request types also require the returned `Status` to be `ACTIVE`.
- Required subject and maximum lengths are validated locally; backend validation remains authoritative.
- Submission is disabled while in progress, sends the documented DTO and one `Idempotency-Key`, and navigates to `/app/tickets/{returnedId}` only after the server confirms success.
- Validation, authentication, authorization, server, network, empty catalogue, and ambiguous network-outcome states are explicit. Ambiguous POST failures are not retried.
- Tokens remain in memory only. No authentication endpoint, bypass, backend change, schema change, or Oracle access was added.

## Verification

Commands run from `client/`:

```text
npm test -- --no-watch --no-progress
npm run build
```

Results:

- Angular tests: **10 files passed, 22 tests passed, 0 failed, 0 skipped**.
- Production build: **passed, 0 warnings, 0 errors**.
- No lint script is configured. Type checking completed through the successful production build.

Tests cover catalogue URL/query contracts, create DTO and idempotency header, required-field validation, catalogue loading, duplicate-submit protection, successful navigation target, validation/401/403/network errors, and no fabricated catalogue or success data.

## Manual verification

1. Start the API at the configured `http://localhost:5018` URL and run Angular with `npm start`.
2. Supply a valid bearer token only through the approved external OIDC handoff.
3. Open `/app/tickets`, choose **Create ticket**, and confirm catalogue requests use `/api/v1/admin/catalogue/...`.
4. Select service, category, subcategory, and request type; confirm downstream values are cleared and reloaded from API responses.
5. Submit once and verify `POST /api/v1/tickets` includes `Idempotency-Key`; confirm navigation occurs only after a returned `TicketView`.
6. Exercise controlled 400/422, 401, 403, 5xx, and network failures. Confirm an ambiguous POST is not retried.

## Limitations and gate status

- Ticket creation implementation: **PASS**
- Catalogue-driven selections: **PASS** for service/category/subcategory/request type
- Dynamic request fields: **BLOCKED by missing field-definition/options read contracts**
- Automated tests: **PASS — 22/22**
- Production build: **PASS**
- Live authenticated creation smoke test: **BLOCKED** — no approved valid bearer/OIDC handoff is configured.
- OIDC end-to-end authentication: **BLOCKED**.

Phase 9.4 is implementation-complete with live authenticated creation explicitly unverified until the external identity-provider contract is supplied.
