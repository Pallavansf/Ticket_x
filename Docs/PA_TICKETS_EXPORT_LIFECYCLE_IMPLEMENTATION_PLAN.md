# PA_TICKETS — Export Lifecycle Implementation Plan

**Review date:** 2026-10-16  
**Status:** review-ready, implementation not approved  
**Acceptance baseline:** 8 PASS / 3 PARTIAL / 13 BLOCKED

This plan consolidates the historical authorization, audit, and cleanup assessments. It does not change application behavior, schema, permissions, grants, scopes, or database state.

## Verified source anchors

- src/PA.Tickets.Api/Controllers/ReportExportsController.cs: authenticated create, status, and download routes; actor comes from NameIdentifier/sub.
- src/PA.Tickets.Infrastructure/Reporting/ReportExportService.cs: export creation, current permission checks, expiry observation, fail-closed download.
- src/PA.Tickets.Infrastructure/Reporting/ReportingQueryService.cs: report definitions, current permission checks, active-user validation, and department/team/client/assignee filtering.
- src/PA.Tickets.Infrastructure/Execution/ReportExportRows.cs: current export fields and absence of authorization snapshot or cleanup state.
- src/PA.Tickets.Infrastructure/Foundations/LocalFileStorage.cs: atomic publication, read, existence check, and best-effort deletion.
- src/PA.Tickets.Infrastructure/Foundations/OperationalMetadataAuditWriter.cs, Persistence/AuditTransaction.cs, and Persistence/AuditChainCoordinator.cs: hash-chain audit conventions and transaction/locking requirements.
- src/PA.Tickets.Infrastructure/Execution/PersistentBackgroundWorker.cs and DurableSlaJobRepositories.cs: durable job leasing, retry, exhaustion, dedupe, and correlation fields.
- tests/PA.Tickets.Domain.Tests/ReportExportAuthorizationTests.cs and ReportExportFormatterTests.cs: focused provider-independent evidence only.

## Reconciled decisions and conflicts

### Historical authorization

Current export rows retain requester, report code, filters, timestamps, status, file hash, storage key, and expiry. They do not retain exact row IDs, effective scope/grant versions, report-definition versions, or field-policy versions. A hash or signed context would prove integrity, not continuing row authorization.

Regeneration under current authorization reduces persistence work but changes original-byte and repeated-download semantics, repeats large queries, and may produce different rows, columns, ordering, or formats after data/definition changes. An authorization-context snapshot preserves the generated artifact but requires row-level evidence, invalidation/version rules, schema/provider work, and a legacy-artifact policy.

**Immediate rule:** downloads remain fail-closed. The owner must choose original bytes versus regenerated output and approve the minimum evidence or regeneration contract before implementation.

### Lifecycle and cleanup

The current persisted statuses are RUNNING, COMPLETED, and FAILED. Expiry is derived from ExpiresAt at read time; it is not currently persisted as a GET side effect. Cleanup eligibility is a proposed operational predicate, not a new export status. Background-job terminal exhaustion is a job state, not an approved export state.

Conceptual lifecycle:

| Phase | Current/proposed behavior | Persistence status |
|---|---|---|
| Request accepted | Create row, validate current permission, begin generation | RUNNING |
| Generation success | Store bytes, hash, size, key, completion time | COMPLETED |
| Generation/storage failure | Bounded failure details | FAILED |
| Retention deadline reached | Deny access immediately from ExpiresAt | Logical expiry; no new persisted state approved |
| Cleanup eligible | Worker claims completed expired artifact | Derived predicate/job lease |
| Delete succeeds or object already absent | Record approved cleanup event and complete job | No new export state approved |
| Delete fails | Retry with durable job policy | Job PENDING/PROCESSING/EXHAUSTED |
| Terminal job exhaustion | Alert/dead-letter and retain access denial | No restoration of access |

The exact linearization point for a download that starts near expiry, and the coordination between opening/streaming and deletion, remain owner and storage-contract decisions. LocalFileStorage does not prove multi-instance coordination.

### Auditing

Proposed unapproved events map to lifecycle points:

- request/row creation: EXPORT_REQUESTED
- successful artifact persistence: EXPORT_COMPLETED
- generation failure: EXPORT_FAILED
- successful authorized response: EXPORT_DOWNLOADED
- denial: EXPORT_ACCESS_DENIED
- logical/durable expiry: EXPORT_EXPIRED
- physical deletion: EXPORT_DELETED
- deletion retry/failure: EXPORT_DELETE_FAILED or EXPORT_CLEANUP_FAILED

Each requires export ID, bounded report identifier where safe, actor or approved system identity, timestamp, outcome/reason category, and correlation/idempotency key. No credentials, tokens, signed URLs, raw filters, response bodies, or unnecessary personal data.

AuditTransaction is atomic only within the owning DbContext transaction. Export state uses ExecutionDbContext while the established writer uses TicketsDbContext; storage is a third boundary. No cross-context or storage atomicity is established. Audit outage behavior must therefore be explicitly approved rather than silently blocking or allowing all operations.

## Implementation dependency graph

### Gate 0 — owner contract

**Approvals:** product/security/data, audit, operations, and provider owners.

Resolve:

- original artifact versus regeneration;
- row-level authorization evidence and invalidation;
- legacy artifacts;
- expiry/download linearization;
- failed/incomplete retention;
- cleanup lease and multi-instance storage guarantees;
- audit event names, worker actor, transaction owner, retry and outage semantics;
- provisional seven-day retention and sensitive-export approval.

**Evidence:** signed decision record and immutable acceptance criteria. No source changes.

### Gate 1 — provider-independent contract tests

**Expected areas:** tests/PA.Tickets.Domain.Tests with fake authorization, clock, storage, audit writer, and job repository.

Cover authorization transitions, expiry, non-disclosure, redaction, event identity, retry/idempotency, cancellation, and race decisions selected at Gate 0.

**Provider dependencies:** none.  
**Rollback:** remove only new tests.  
**Deferred:** provider persistence, locking, migration, and E2E.

### Gate 2 — authorization implementation

**Expected areas:** ReportExportService, reporting contracts/query path, formatter/download controller only as required by the approved strategy.

- Regeneration requires bounded query/format behavior, repeated-download semantics, timeout/size handling, and legacy-filter denial.
- Snapshot requires approved persistence/context model, integrity protection, invalidation, and compatibility handling.

**Required gate:** no download enablement until tests prove the selected strategy’s row/field authorization contract. Unsupported legacy artifacts remain denied.

### Gate 3 — audit integration

**Expected areas:** export service, approved audit adapter/writer, transaction boundary, and worker handlers.

Use existing hash-chain infrastructure where the owning context can support it. If cross-context durable delivery is required, prepare a separately approved persistence/outbox design; do not introduce one implicitly.

**Required gate:** event idempotency, actor semantics, redaction, and audit-failure behavior are tested and owner-approved.

### Gate 4 — asynchronous cleanup

**Expected areas:** existing durable worker registration, a future export cleanup handler, job repository use, storage abstraction, and approved audit integration.

The handler must claim due work safely, enforce expiry independently, delete idempotently, classify missing objects, retry transient failures, dead-letter exhausted jobs, and never restore access.

**Required gate:** storage and multi-instance concurrency contract, cleanup monitoring, retry/dead-letter ownership, and cancellation behavior are approved.

### Gate 5 — provider/schema phase

Only after the existing migration gate:

- reconcile actual catalog and deployed migration baseline;
- approve exact schema/context/audit changes;
- capture immutable file hashes, backup/recovery evidence, preflight and preservation checks;
- run isolated PostgreSQL/Oracle persistence, transaction, locking, and rollback tests.

### Gate 6 — API/browser validation and release

Run hosted API and Angular/browser journeys only after provider evidence. Verify download authorization, expiry, cleanup, audit visibility, non-disclosure, and no unintended mutation. Mocked/provider-independent tests cannot be promoted to database or full E2E evidence.

## Release gates and evidence

### Enable downloads

- Approved strategy and product semantics.
- Proof that current permission/account/scope checks plus the selected regeneration or snapshot evidence establish every returned row and field.
- Legacy-row handling and non-disclosure tests.
- Focused API build/tests passing.
- No claim of provider readiness without provider evidence.

### Enable asynchronous cleanup

- Approved retention and cleanup scope.
- Durable claim/lease and dedupe evidence.
- Storage delete/open concurrency contract.
- Retry, cancellation, missing-object, crash-recovery, and alerting tests.
- Audit actor/event/failure policy approved.

### Claim audit completeness

- Event catalogue and actor semantics approved.
- Durable persistence/transaction or approved retry/outbox evidence.
- Hash-chain verification and duplicate-event interpretation.
- Redaction and audit-access controls verified.
- Provider-backed transaction evidence completed.

### Approve database migrations

- Authoritative target fingerprint and environment.
- Reviewed immutable migration order and hashes.
- Known deployed baseline or approved adoption procedure.
- Backup/recovery and rollback plan.
- Pre/post preservation checks for users, memberships, grants, exports, tickets, and audit history.
- Explicit approval of exact target and operations.

### Declare production readiness

- All prior gates complete.
- Provider and hosted E2E evidence archived.
- Cleanup monitoring and incident procedures active.
- Acceptance matrix updated only from reproducible evidence.

JSON API exposure is outside this plan and remains a later feature.

## Compatibility and rollback

Until Gate 2 is complete, current downloads remain fail-closed. Existing role, permission, scope, report, and file-format behavior is preserved. Any snapshot schema or audit persistence change requires a backward-compatible read policy, preservation of legacy rows, explicit rollback, and no fabricated historical authorization. Cleanup must never restore access after expiry. Strategy changes after release require an owner-approved compatibility plan for already-created exports.

## Validation limitations

This consolidation used source and documentation inspection only. No database, provider, migration, provisioning, fixture, cleanup job, or database-backed test was run. No application behavior or schema changed. Historical downloads remain fail-closed where authorization cannot be established. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


## Owner approval checklist cross-reference (2026-10-16)

`Docs/PA_TICKETS_EXPORT_LIFECYCLE_OWNER_APPROVAL_CHECKLIST.md` is the compact decision gate for authorization, auditing, cleanup, retention, provider execution, and E2E. No item is approved; downloads remain fail-closed and acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.
