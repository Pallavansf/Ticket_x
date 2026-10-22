# PA_TICKETS — Export Lifecycle Remaining Gaps

**Review date:** 2026-10-16  
**Review type:** source and documentation inspection only  
**Acceptance baseline:** 8 PASS / 3 PARTIAL / 13 BLOCKED

This assessment records remaining lifecycle gaps; no application behavior, schema, grants, or database state was changed.

## Confirmed current behavior

The safe subset is in src/PA.Tickets.Infrastructure/Reporting/ReportExportService.cs.

- CreateAsync resolves the report definition, enforces its current permission through EnsureCurrentReportPermissionAsync, records report code, requester, filters, correlation ID, lifecycle status, storage metadata, and expiry, executes through IReportingQueryService.QueryAsync, and marks failures as FAILED.
- GetAsync restricts lookup to Id == exportId && RequestedBy == actor, rechecks current report permission, and uses ToStatusAtRead. A completed row past ExpiresAt is presented as EXPIRED in memory; ordinary GET performs no status write or file deletion.
- DownloadAsync applies owner and current-permission checks, denies expired, failed, incomplete, or missing-storage exports, then fails closed with UnauthorizedAccessException because persisted metadata cannot prove every historical row remains authorized.
- src/PA.Tickets.Infrastructure/Execution/ReportExportRows.cs stores report code, format, requester, filters JSON, lifecycle timestamps, row count, file size, content type, hash, storage key, failure details, correlation ID, and expiry. It has no row identifiers, authorization snapshot, scope IDs, membership/effective-grant version, report-definition version, or field-permission version.
- src/PA.Tickets.Infrastructure/Foundations/LocalFileStorage.cs atomically publishes temporary files. OpenReadAsync, ExistsAsync, and DeleteAsync operate on local files; deletion is best effort and suppresses errors.
- ExecutionDbContext maps exports, jobs, and Audits. The established chained writer is OperationalMetadataAuditWriter, using AuditChainCoordinator and AuditTransaction against TicketsDbContext; cross-context transaction and export-event ownership are not established.
- PersistentBackgroundWorker leases durable BackgroundJobRow records and dispatches IBackgroundJobHandler implementations. ReportScheduleJobHandler is the existing report handler. No export-expiry cleanup handler was found.

These are static findings only; they do not establish provider behavior, deployed schema, or concurrency guarantees.

## Historical-artifact authorization

Current metadata cannot reconstruct the authorization context that produced each row. Current TICKET_READ and scope rechecks therefore cannot prove that an old artifact contains no rows made unauthorized by permission revocation, department/team/client/assignee scope reduction, account deactivation, report-definition changes, or field-permission changes. Downloads must remain fail-closed until a design is approved.

### Viable designs

| Design | Security | Compatibility/performance | Persistence impact |
|---|---|---|---|
| Regenerate on download under current authorization | Re-evaluates rows and fields at retrieval; safest without trusting old bytes | Changes snapshot semantics, adds latency and query cost, and requires a regeneration response contract | May avoid a new table, but needs service/API changes |
| Approved authorization-context snapshot | Binds artifact to effective permission, membership, scope, report-definition and field-policy context; may retain row/scope evidence or a verifiable context hash | Preserves generated snapshot and fast download, but adds storage, migration, and retention cost | Requires approved schema/persistence design and provider migration review |
| Owner-bound artifact | Simple and compatible | Permits retrieval of data the owner can no longer query; conflicts with current-access policy | No schema change, but requires explicit risk approval |

Recommendation for review: retain fail-closed downloads and choose either regeneration under current authorization or an explicitly approved authorization-context snapshot. Do not implement until evidence, scope semantics, definition/field versioning, and legacy-row behavior are approved.

## Audit lifecycle contract proposal

Reuse the established audit architecture; do not create a parallel system. Candidate (unapproved) events are: request/row creation; execution success/failure; download success; authorization denial; expiry observed/applied; physical deletion; cleanup failure/retry.

Events should carry export ID, report code where safe, approved actor or legacy/unknown representation, UTC time, outcome/reason code, and correlation/idempotency key. Never log credentials, signed URLs, full filters, file contents, or unnecessary personal data.

The code does not prove that ExecutionDbContext.Audits and TicketsDbContext audit chains share a transaction or chain namespace. Owner decisions are required for event identifiers, actor semantics, authoritative context, and transaction/outbox coupling.

Failure options are fail operation, complete with durable retry/outbox, or best effort. Recommended default for approval: authorization denials remain fail-closed; successful creation/download is not reported complete until its required audit is durably captured or an approved outbox makes recovery deterministic. Use a stable idempotency key such as export-id + event-kind + lifecycle-attempt so retries cannot silently duplicate business events.

## Asynchronous cleanup and concurrency

Expiry authorization must remain independent of physical deletion, and ordinary GET requests must not mutate rows or delete storage. The durable worker and BackgroundJobRow are possible infrastructure for a future cleanup handler, but the current code has no cleanup job type, export claim query, or download-coordinated state transition.

Local storage proves atomic publication, not concurrent deletion safety: OpenReadAsync uses FileShare.Read, while DeleteAsync best-effort deletes and suppresses errors. No shared lease, distributed conditional delete, or database row-version transition is evidenced. Concurrency safety therefore cannot be claimed.

A future implementation must make cleanup idempotent; treat missing files as an explicit terminal condition; retain retryable storage failure state; coordinate cleanup with an atomic lease/state or storage primitive; avoid non-owner existence disclosure; and prevent deletion of a replacement object (current keys include export ID and a generated suffix).

## Owner decisions and blockers

1. Select regeneration versus authorization-context snapshot, including row/scope evidence and existing-artifact behavior.
2. Define scope dimensions and report/field-definition version semantics at download.
3. Approve audit event identifiers, actor representation, payload minimization, transaction/outbox coupling, and audit-failure behavior.
4. Approve cleanup owner, cadence, retry/dead-letter policy, deletion state machine, and storage concurrency guarantees.
5. Confirm seven-day retention; it remains provisional.
6. Decide whether sensitive exports require independent approval. No new export permission is proposed.
7. Approve provider/schema work only after catalog, baseline, backup, and rollback evidence; no provider compatibility is established here.

## Minimal implementation sequence after approval

1. Record the owner contract and immutable acceptance criteria.
2. Add provider-independent tests with fake authorization, definitions, scopes, clock, storage, and audit writer.
3. Implement the selected artifact authorization design while retaining fail-closed behavior for unsupported legacy rows.
4. Integrate export events through the existing audit architecture or approved outbox with idempotency and failure semantics.
5. Add a durable cleanup handler using the existing worker only after claim/lease and storage consistency rules are approved.
6. Run provider-specific migration/transaction tests in a separately approved database phase.
7. Run API/Angular E2E only after that phase; mocked tests are not database verification.

## Required provider-independent regression tests

- owner status allowed with active permission; anonymous, non-owner, inactive, and missing requests are non-disclosing;
- permission revoke/regrant, account deactivate/reactivate, and scope reduction/expansion;
- changed report definition and field permissions;
- insufficient historical context remains denied;
- expired, failed, incomplete, missing, corrupt, and partially deleted files;
- repeated cleanup idempotency, storage retry, and deterministic missing-object handling;
- deterministic concurrent cleanup/download race;
- audit success, failure, retry, and duplicate-event interpretation;
- ordinary GETs perform no expiry write or storage deletion.

## Validation limits

Inspection was limited to checked-in source and documentation. No database, provider, migration, storage service, or full integration test was contacted. Prior evidence remains five focused provider-independent tests passing and an API build with zero warnings/errors; this assessment adds no runtime verification. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


## Historical authorization design cross-reference (2026-10-16)

`Docs/PA_TICKETS_HISTORICAL_EXPORT_AUTHORIZATION_DESIGN.md` provides the detailed source-based comparison of regeneration versus authorization-context snapshots. The owner decision remains unresolved and downloads remain fail-closed.

## Audit contract assessment cross-reference (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_AUDIT_CONTRACT_DESIGN.md` for the source-only event, actor, transaction, failure, privacy, and retry contract assessment. No audit or cleanup behavior is implemented.

## Cleanup design cross-reference (2026-10-16)

See `Docs/PA_TICKETS_EXPORT_CLEANUP_DESIGN.md` for the source-only expiry, worker, storage, race, retention, and cleanup assessment. No cleanup behavior is implemented.
