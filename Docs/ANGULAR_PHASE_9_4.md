# PA Tickets Phase 9.4 — Ticket creation and catalogue-driven forms

Phase 9.4 implementation remains in `client/src/app/features/tickets/create`.

- Creation: `POST /api/v1/tickets` with the documented `CreateTicketRequest` and required `Idempotency-Key`.
- Catalogue reads: `/api/v1/admin/catalogue/services`, `/categories`, `/sub-categories`, and `/request-types`.
- The form supports ticket type, subject, description, and dependent catalogue selections.
- Dynamic request fields remain unavailable because the backend exposes field associations but no field-definition/options read contract.
- The Angular tests and production build were verified during Phase 9.4; OIDC live creation remained blocked.
