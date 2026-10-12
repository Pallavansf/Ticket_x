# Auditor Reporting (Phase 8.5)

This is a read-only, controlled reporting API for callers granted `AUDITOR_VIEW`. It composes persisted operational records and does not create a second audit system, mutate data, execute caller SQL, expose EF entities, or provide an ISO certification mechanism.

## Endpoints

- `GET /api/v1/auditor/reports`
- `GET /api/v1/auditor/reports/{reportCode}`
- `POST /api/v1/auditor/reports/{reportCode}/query`
- `GET /api/v1/auditor/reports/tickets/{ticketId}/integrity`
- `GET /api/v1/auditor/reports/configuration/{entityType}/{entityId}/integrity`

The executable catalogue contains `AUDIT_ACTIVITY`, `TICKET_AUDIT_TRAIL`, `WORKFLOW_AUDIT`, `APPROVAL_AUDIT`, `ASSIGNMENT_AUDIT`, `EVIDENCE_AUDIT`, and `COMMUNICATION_AUDIT`. `SECURITY_AUDIT` is not exposed because no general persisted security-event store exists in the current model.

Queries use Asia/Kolkata date-only boundaries with start inclusive/end exclusive semantics, deterministic timestamp/ID ordering, allow-listed sorting, and page size 25 by default with a maximum of 100. Authorization is checked before querying and ticket scope predicates are applied to the actual queries.

Evidence results expose metadata only; attachment contents and storage references are never returned. Communication results expose delivery metadata and failure classification, not message bodies, credentials, or provider secrets.

The ticket integrity route recomputes the `TICKET` stream hash and predecessor chain after applying ticket-scope authorization. The configuration integrity route requires `AUDITOR_VIEW` and currently accepts only global `STATUS_DEFINITION` and `PRIORITY_DEFINITION` streams. Both routes are read-only and return integrity status/count/failure metadata, not old/new values, reasons, or stored hashes. Unsupported configuration formats are not found; recognized but empty streams return `NO_AUDIT_RECORDS`. Historical canonical formats and reconstruction limitations are documented in `AUDIT_HASH_DESIGN.md`.

Authoritative sources are the existing ticket audit/event, workflow history, approval history, assignment, evidence/attachment, and communication tables. Security event reporting, arbitrary entity browsing, exports, scheduled reports, snapshots, caching, ETL, Angular UI, and Oracle execution remain deferred.
