# Export and Scheduled Reporting

Phase 8.6 provides controlled exports and durable scheduled report generation over the existing reporting query services.

## APIs

All business routes use `/api/v1`:

- `POST /reports/{reportCode}/exports`
- `GET /exports/{exportId}`
- `GET /exports/{exportId}/download`
- `GET|POST /report-schedules`
- `GET|PUT /report-schedules/{scheduleId}`
- `POST /report-schedules/{scheduleId}/pause|resume|cancel`
- `GET /report-schedules/{scheduleId}/executions`

Exports and schedules are restricted to the authenticated owner and the existing report permission and ticket scope are re-evaluated when report rows are queried. A schedule does not grant elevated access.

## Formats and limits

CSV and XLSX are supported. CSV uses deterministic report columns, UTF-8 encoding, ISO date/time values, invariant numeric values, null-as-empty handling, RFC-style escaping, and a leading apostrophe for string cells beginning with `=`, `+`, `-`, or `@` to prevent spreadsheet formula execution. XLSX is a bounded, dependency-free OOXML workbook with one deterministic worksheet, inline safe values, no macros, and no external links. PDF is rejected because no supported PDF dependency is present; no placeholder files are returned.

An export is limited to 10,000 rows and 25 MiB. The service fetches controlled pages of 100 rows, checks the reported total, and publishes only a complete result. Cancellation, query failures, and limit violations leave a durable failed export and do not publish a partial file.

## Storage and lifecycle

Exports use the existing local file storage abstraction. Files are written to a temporary name and atomically moved below the configured storage root only after generation succeeds. Database metadata stores the generated key, content type, size, SHA-256 hash, correlation id, actor, status, and expiration; physical paths are never returned. Completed files expire after seven days and become `EXPIRED` before download. Local storage is suitable for the current single-instance development setup only; production multi-instance deployments require shared durable storage and an operational cleanup policy.

## Schedules

Schedules persist daily, weekly, or monthly recurrence, an explicit time zone (default `Asia/Kolkata`), normalized filters, owner, status, and next run. Invalid local times are advanced by one hour; ambiguous times use the time-zone offset selected by the platform. Monthly day values clamp to the last day of shorter months. The existing durable background worker enqueues due schedules with a stable dedupe key and invokes the same report/export path used by on-demand requests. Pause, resume, cancel, restart recovery, retries, and execution history are durable database state.

Scheduled delivery to arbitrary email recipients is not enabled in this phase. Generated files are retrieved through the authorized export download endpoint.

## Reliability, audit, and deployment

Exports use quarantine storage and terminal `COMPLETED`, `FAILED`, and `EXPIRED` states. Durable job retry and failure classification are reused; transient worker failures retry within configured limits and permanent failures remain recorded. PostgreSQL schema is in `database/migrations/026_phase8_6_reporting.sql` and must be provisioned before use. Oracle is intentionally untouched. Retention periods and shared storage requirements must be aligned with organizational policy before production deployment.