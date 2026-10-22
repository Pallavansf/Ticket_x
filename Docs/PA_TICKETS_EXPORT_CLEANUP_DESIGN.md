# PA_TICKETS — Export Expiry and Physical Cleanup Design

**Review date:** 2026-10-16  
**Review type:** source-only assessment  
**Acceptance baseline:** 8 PASS / 3 PARTIAL / 13 BLOCKED

No application behavior, schema, permissions, or cleanup behavior was changed.

## Confirmed lifecycle behavior

In src/PA.Tickets.Infrastructure/Reporting/ReportExportService.cs:

- CreateAsync creates a ReportExportRow as RUNNING, sets CreatedAt/StartedAt and ExpiresAt to creation time plus a seven-day constant, queries pages, formats the artifact, writes storage, then marks the row COMPLETED with hash, size, content type, row count, and storage key.
- Generation or storage exceptions mark the row FAILED with a bounded failure message and classification. Cancellation is classified as CANCELLED, but the persistence of that failure is attempted with CancellationToken.None and is not proven durable without a database.
- GetAsync performs a non-disclosing owner lookup and current permission check. ToStatusAtRead presents a completed row past ExpiresAt as EXPIRED in memory only. Ordinary GET does not write expiry state or delete storage.
- DownloadAsync performs owner and current permission checks, denies expired/non-completed/missing-storage rows, and then fails closed because current metadata cannot establish historical row authorization.
- ReportExportRow has no cleanup state, deletion-attempt timestamp, lease, row version, or deletion outcome field.
- LocalFileStorage publishes through a temporary file and atomic move. OpenReadAsync opens with FileShare.Read; ExistsAsync checks the path; DeleteAsync best-effort deletes and suppresses errors. This does not establish cross-process deletion coordination.

## Existing worker capabilities

PersistentBackgroundWorker polls every 30 seconds, leases ready BackgroundJobRow records through DurableBackgroundJobRepository, dispatches IBackgroundJobHandler, and marks jobs complete or failed. Lease state, attempts, retry delay, max attempts, dead-letter/exhausted status, correlation ID, and worker ID are persisted.

BackgroundJobFailureClassifier treats cancellation and selected transient failures as retryable and argument/invalid-operation/unauthorized errors as non-retryable. This infrastructure can host a future cleanup handler, but no export cleanup job type, export claim query, or cleanup state machine exists today. Lease guarantees are database/provider dependent and were not executed or verified.

## Safe cleanup lifecycle proposal

The minimum defensible behavior is:

1. ExpiresAt remains the authoritative access cutoff. Status/download authorization must deny after that instant, regardless of physical file presence.
2. Ordinary status/download GETs remain read-only with respect to expiry and storage.
3. A separately scheduled cleanup job identifies completed exports whose ExpiresAt is past and whose storage key belongs to that export.
4. Cleanup claims work using an approved durable lease/state transition. Do not rely on a process-local lock for multiple application instances.
5. The handler attempts idempotent deletion. A missing object is recorded as an explicit already-absent outcome, not as renewed access.
6. Successful deletion and failure/retry outcomes are recorded through the approved audit contract and durable job state.
7. A cleanup crash is recoverable: a later attempt may repeat deletion safely, and an expired artifact never becomes downloadable again.
8. Failed, incomplete, and queued rows require an owner-approved policy. Do not infer that all non-completed rows should be deleted or retained indefinitely.

No new state or persistence field is settled by this assessment. A durable cleanup marker or deletion-attempt state may be required, but that is a schema/provider decision.

## Race and failure analysis

| Scenario | Confirmed risk | Minimum required guarantee |
|---|---|---|
| Download starts just before expiry | It may have passed authorization before the cutoff; source does not define stream cutoff semantics | Owner-defined linearization point; no new access after expiry |
| Cleanup starts while download opens/streams | Local FileShare.Read behavior does not prove safe cross-provider semantics | Shared lease/atomic object-read or documented storage guarantee |
| Multiple cleanup workers | Durable job leases reduce duplicate job processing, but export-level uniqueness is unproven | Deterministic dedupe and export claim/lease |
| Worker crashes after delete before completion record | File may be gone while job remains retryable | Idempotent missing-object handling and compensating audit |
| Delete succeeds, persistence update fails | Physical and logical state diverge | Expiry remains access gate; durable retry/reconciliation |
| Persistence update succeeds, delete fails | Artifact remains physically present but inaccessible | Retryable storage failure and monitoring |
| Cancellation during delete | Local DeleteAsync ignores cancellation | Define cancellation as retryable/unknown outcome; do not restore access |
| Audit fails after deletion | Irreversible action lacks audit evidence | Approved audit/outbox or explicit compensating event; no false atomicity |

A process-local mutex can reduce duplicate work inside one process only. It cannot coordinate multiple application instances, workers, storage providers, or crashes.

## Audit integration

Use the event proposals in PA_TICKETS_EXPORT_AUDIT_CONTRACT_DESIGN.md: expiry applied/observed, deletion completed, deletion failed/retry, and cleanup execution failure. The worker actor must use an approved system representation compatible with the non-null audit actor column; never attribute cleanup to an administrator.

The current audit writer is tied to TicketsDbContext and its hash-chain transaction. Cleanup state is in ExecutionDbContext and storage is separate. Source does not prove atomicity across these stores. Audit success/failure must therefore be handled under an approved transaction/outbox or compensating-event contract, not assumed atomic.

## Retention and operations

The current ExpiresAt is CreatedAt plus seven days. This retention is provisional and must not be silently finalized as an audit retention period.

Owner decisions are required for:

- failed/incomplete/queued export retention and cleanup;
- worker cadence and disabled-worker behavior;
- retry/dead-letter thresholds and alerting;
- missing-object and orphan-object reconciliation;
- cleanup metrics, dashboards, and incident escalation;
- storage durability and multi-instance semantics;
- download/expiry linearization;
- seven-day retention and any sensitive-export approval.

If the worker is unavailable, access must still be blocked after ExpiresAt. Physical files may remain until operations restore cleanup; this is an operational risk, not renewed authorization.

## Minimal implementation sequence after approval

1. Approve retention, cleanup scope, state/lease semantics, and race linearization.
2. Add provider-independent tests around a fake clock, storage, job repository, and audit writer.
3. Implement a cleanup handler using existing durable worker infrastructure only after export claim semantics are approved.
4. Add durable retry/dead-letter and bounded diagnostics.
5. Integrate cleanup audit events through the approved actor and transaction/outbox contract.
6. Prepare provider/schema changes only after migration baseline, backup, and rollback approval.
7. Run provider and E2E phases separately.

## Provider-independent regression tests

- expiry blocks status/download immediately without GET writes;
- repeated cleanup is idempotent;
- missing object is deterministic and non-authorizing;
- deletion/storage failures retry and eventually dead-letter;
- cancellation during deletion does not restore access;
- worker crash/retry after deletion is safe;
- two cleanup attempts cannot corrupt state;
- deterministic download/cleanup race behavior;
- cleanup actor and correlation propagation;
- audit success/failure behavior;
- failed/incomplete export policy follows the approved contract.

Provider-dependent tests are deferred for job leasing SQL, transaction isolation, export claim locking, provider-specific cleanup state, storage durability, and migration/rollback.

## Validation limits

Only checked-in source and documentation were inspected. No database, provider, migration, cleanup job, storage service, or database-backed test was accessed. No application behavior or schema changed. Historical downloads remain fail-closed where authorization cannot be established. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

