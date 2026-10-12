# Phase 7.4 — Operational reliability and observability

## Audit findings

Confirmed that durable jobs had exponential scheduling but retried every exception without a maximum, and expired jobs could be re-leased indefinitely. Communication delivery already rejected the deterministic provider in production, but manual retries could be repeated and HTTP transport errors were classified as permanent. Ticket communication history was unbounded and performed one latest-attempt query per row. The API had one generic `/health` route without dependency-aware readiness.

## Implemented

- Durable jobs persist maximum attempts, failure classification, first/last failure timestamps, last attempt, terminal failure timestamp, and correlation ID. Attempts default to five and can be configured with `Jobs:Retry:MaxAttempts` (bounded to 1–20).
- Transient failures include provider timeouts, HTTP transport errors, and provider-classified transient database errors. Invalid payload/configuration, authorization, malformed JSON, and other permanent errors stop immediately.
- Retry delay grows exponentially from `Jobs:Retry:InitialDelaySeconds` (default 5) to `Jobs:Retry:MaxDelaySeconds` (default 3600, hard maximum 86400). Retry state and due time live in PostgreSQL.
- The existing `EXHAUSTED` durable terminal status is used after permanent failure or maximum attempts; exhausted expired leases are terminalized and cannot be leased again. Successful jobs remain non-releasable.
- Communication retries are capped by `Communication:Retry:MaxAttempts`; permanent failures and maximum-attempt failures cannot be manually requeued. HTTP transport and timeout failures are retryable. Production provider safety remains fail closed.
- `ForTicketAsync` returns the latest 100 communications, and latest attempts are batch-loaded. The existing `(N_COMMUNICATION_ID, N_ATTEMPT_NO)` index supports the lookup.
- `/health/live` checks process liveness only. `/health/ready` checks both database contexts with a two-second timeout and worker polling when workers are enabled. `/health` remains as a readiness alias. Responses contain no connection details.
- Request correlation accepts only bounded safe ASCII IDs and generates an ID otherwise. It is returned in `X-Correlation-ID`, used as the trace identifier, and included in structured request logs.
- The `PA.Tickets.Operational` .NET Meter/ActivitySource exposes request counts/failures/duration; job queued/leased/completed/failed/dead-letter/retry counts and duration; communication attempt/delivery/failure/retry counts; worker poll/failure/availability; and database failure/availability instruments. Labels are limited to HTTP method/status, internal job type, channel, outcome, and failure class.

## Database and provider notes

PostgreSQL migration `025_phase74_operational_reliability.sql` adds durable retry fields and a partial lease-expiry index. The existing communication-attempt composite index is sufficient; no duplicate index was added. The PostgreSQL test provisioner includes this migration.

Oracle was not connected to and no Oracle migration was run or changed. Before Oracle is re-enabled, an Oracle 19c additive migration must add the six job columns and equivalent indexes. Oracle schema deployment remains deferred.

## Remaining deployment conditions

- Production OIDC authority/issuer and audience must be configured.
- Explicit production CORS origins must be configured.
- Trusted proxy IPs must be configured where TLS terminates behind a proxy.
- A real production malware scanner is required.
- A real production communication provider is required.

Phase 7.4 did not add API versioning, `/api/v1`, reporting, dashboards, or Phase 8 deployment automation.