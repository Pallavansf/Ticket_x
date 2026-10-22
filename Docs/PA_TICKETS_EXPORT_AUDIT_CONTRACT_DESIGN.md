# PA_TICKETS — Export Lifecycle Audit Contract Design

**Review date:** 2026-10-16  
**Review type:** source-only assessment  
**Acceptance baseline:** 8 PASS / 3 PARTIAL / 13 BLOCKED

No application, schema, permission, scope, or audit behavior is changed by this document.

## Current audit architecture

- AuditRow is defined in src/PA.Tickets.Infrastructure/Persistence/TicketsDbContext.cs. It has entity type, entity ID, action, required numeric ActorId, timestamp, old/new values, reason, hash, and previous hash.
- TicketsDbContext and ExecutionDbContext both expose an Audits set mapped to PA_TICKETS_AUDIT. Source does not establish that the two contexts share a transaction or that concurrent writes across contexts preserve one chain.
- OperationalMetadataAuditWriter.AppendAsync uses TicketsDbContext, AuditTransaction, AuditChainCoordinator, a provider-specific lock row, and SHA-256 chaining. It starts a transaction only when the context has none, acquires the entity chain lock, reads the previous hash, inserts the row, saves, and commits an owned transaction.
- AuditChainCoordinator requires an active transaction and uses provider-specific INSERT/MERGE plus FOR UPDATE locking. The established conventions therefore require audit persistence in the same context/transaction that owns the business change.
- Existing services generally pass a numeric authenticated actor into audit rows. ReportExportsController obtains that actor from NameIdentifier or sub and rejects an unmappable identity. No approved system/worker actor representation is established for export cleanup.
- PersistentBackgroundWorker leases BackgroundJobRow records and dispatches IBackgroundJobHandler. It logs worker activity and retries according to BackgroundJobFailureClassifier. No export audit handler or cleanup event writer exists.
- AuditorReportingService reads audit records by actor, action, entity, date, and correlation-like Reason. AuditRow has no dedicated correlation column; existing code sometimes stores correlation in Reason.

Confirmed: audit failures from OperationalMetadataAuditWriter propagate as exceptions to the caller. Confirmed: some business services include audit writes in their own transaction. Unverified: every call site, deployed foreign-key constraints, retention policy, and cross-context atomicity. Audit method invocation alone does not prove durable persistence.

## Proposed export event catalogue

Names below are proposals only and require owner approval.

| Lifecycle point | Proposed event | Actor | Minimum metadata | Existing chain? |
|---|---|---|---|---|
| Request accepted/row created | EXPORT_REQUESTED | authenticated user | export ID if allocated, report code, correlation ID, time, outcome | Existing audit chain if transaction can include export state |
| Artifact completed | EXPORT_COMPLETED | authenticated user or scheduled-job owner, explicitly identified | export ID, report code, format, row count, size, correlation ID, time | Same chain/transaction contract required |
| Generation failed | EXPORT_FAILED | initiating user or system worker | export ID, report code, failure category, correlation ID, time | Same chain where possible |
| Download returned | EXPORT_DOWNLOADED | authenticated user | export ID, report code, correlation ID, time, outcome | Existing chain only after transaction contract is approved |
| Access denied | EXPORT_ACCESS_DENIED | authenticated user when known; approved unknown/system representation otherwise | attempted export ID, reason category, correlation ID, time; no existence disclosure in response | Chain or approved security operational log decision required |
| Expiry applied/observed | EXPORT_EXPIRED | system worker for durable transition; no user attribution invented | export ID, expiry time, correlation ID/job ID, outcome | Existing chain with approved worker actor |
| Physical deletion completed | EXPORT_DELETED | system worker | export ID, storage provider class, time, job/correlation ID, outcome | Existing chain if durable deletion state is transactional |
| Physical deletion failed/retry | EXPORT_DELETE_FAILED | system worker | export ID, failure category, retry state, job/correlation ID, time | Chain plus operational retry diagnostics |
| Cleanup execution failed | EXPORT_CLEANUP_FAILED | system worker | job ID, bounded failure category, retry/dead-letter state, time | Existing chain or worker operational log, owner decision |

Do not log raw filters, response rows, file contents, signed URLs, credentials, tokens, storage paths that disclose secrets, or unnecessary personal data. If investigation requires filter provenance, store a normalized allowlisted filter summary or keyed digest only after privacy and collision/forensics requirements are approved; a digest does not reveal the filter and does not prove authorization.

## Actor and correlation contract

User events must retain the authenticated numeric user identity already used by AuditRow. Background events require an explicit legacy/system actor representation compatible with the non-null ActorId column and its foreign-key behavior; no current administrator may be substituted. The owner must define whether a reserved system principal, nullable compatibility change, or separate operational record is permitted.

Every event should carry a stable export ID where one exists and a correlation/idempotency key. Existing audit readers may need to continue using Reason for correlation until an approved schema contract exists. Event payloads should use bounded reason and failure categories rather than exception text containing sensitive data.

## Failure-semantics matrix

| Failure | Security/integrity concern | Current evidence | Decision required |
|---|---|---|---|
| Request succeeds, request event fails | action exists without audit evidence | no export audit writer | Decide fail request, rollback, or durable retry |
| Generation succeeds, completion event fails | artifact may be downloadable without completion evidence | no event implementation | Prefer no “completed” state until durable event or approved outbox; owner must approve |
| Generation and failure event both fail | incident and outcome are ambiguous | current service persists FAILED but no audit | Preserve FAILED state and bounded diagnostics; define retry/alert policy |
| Download succeeds, event fails | data disclosure is complete but audit absent | download currently always fails closed | Decide whether response is blocked or event is retried durably |
| Denial occurs, denial event fails | security investigation gap | authorization exceptions propagate | Keep denial fail-closed; define audit outage behavior and alerting |
| Cleanup deletes file, deletion event fails | irreversible physical action lacks evidence | no cleanup path | Require durable deletion intent/state or approved compensating event |
| Retry repeats event after partial failure | duplicate audit records or ambiguous history | no export idempotency key | Stable event key and duplicate interpretation required |
| Audit service unavailable | availability versus accountability trade-off | no export-specific policy | Security/product owner must classify event criticality |

Recommended review default: authorization denial remains fail-closed and must not disclose export existence. For successful creation, completion, and deletion, do not claim durable audit semantics until transaction or durable-retry ownership is approved. Download response behavior when its audit event fails is explicitly unresolved because blocking protects accountability but can create availability and client-retry duplicates.

## Transaction, retry, and consistency analysis

AuditTransaction can join an existing DbContext transaction or create and commit its own. That supports atomic business-plus-audit work only when both are in the same context and transaction. Report exports currently use ExecutionDbContext for state while the established writer uses TicketsDbContext; source does not prove a shared transaction or chain-safe cross-context write.

The durable worker provides leases, attempts, retry scheduling, and dedupe keys for jobs, but does not provide an audit outbox. A future implementation may use the worker only after an approved durable event/state contract; adding a new outbox table or schema is not approved here.

Event identity should be deterministic, for example export ID + event kind + lifecycle attempt. Retries must be idempotent or explicitly marked as repeated attempts. A file deletion followed by audit failure cannot be made atomic with local storage using the current evidence; it needs an approved state machine, compensating audit, or durable deletion record.

## Privacy and security controls

- Keep responses non-disclosing for non-owners and unknown export IDs.
- Record identifiers, bounded reason codes, timestamps, actor class, and correlation IDs only.
- Never record credentials, JWTs, signed URLs, full report filters, response bodies, file bytes, or unnecessary personal data.
- Preserve the existing hash-chain conventions and do not rewrite historical records.
- Separate user actions from automated worker actions.
- Apply explicit audit access controls and retention; seven-day export-file retention is not an audit-retention decision.
- Redact exception messages before audit or operational logging.

## Owner decisions and provider/schema blockers

1. Approve event names, entity type, action values, and required metadata.
2. Approve system-worker actor semantics compatible with non-null ActorId and existing foreign keys.
3. Decide whether export audit rows belong to the existing chain, and which DbContext/transaction owns them.
4. Decide fail/block/retry semantics for completion, download, denial, deletion, and audit outages.
5. Approve deterministic event identity and duplicate-event interpretation.
6. Approve filter provenance representation, if any.
7. Approve retention, audit-reader permissions, and alerting.
8. Approve any schema, outbox, cross-context transaction, or provider-specific changes only after catalog, baseline, backup, and rollback evidence.

## Minimal implementation sequence after approval

1. Freeze the event and actor contract without changing existing history.
2. Add provider-independent contract tests with fake audit writers and deterministic clock/correlation IDs.
3. Integrate the chosen existing audit writer/transaction path for export lifecycle events.
4. Add bounded redaction, idempotency, retry, and failure handling.
5. Design any required schema/provider work under the existing database approval gate.
6. Add cleanup auditing only with the separately approved cleanup state/concurrency contract.
7. Run provider and E2E phases separately; do not infer persistence from unit tests.

## Provider-independent regression tests

- correct event selection and minimum metadata for every lifecycle outcome;
- user actor and system-worker attribution without substituting an administrator;
- secret, URL, filter, response, and exception redaction;
- correlation propagation and deterministic event identity;
- audit failure behavior for request, completion, failure, denial, download, expiry, deletion, and cleanup;
- duplicate and retry interpretation;
- non-disclosing unauthorized responses;
- unchanged business behavior when audit succeeds;
- explicitly approved behavior when audit persistence fails.

Provider-dependent tests, deferred: transaction atomicity, hash-chain locking, actor foreign keys, cross-context consistency, migration/rollback, PostgreSQL/Oracle SQL behavior, and durable worker persistence.

## Validation limits

Only checked-in source and documentation were inspected. No database, provider, migration, cleanup, or database-backed test was accessed. No application behavior or schema changed. Historical-artifact authorization remains a separate unresolved decision. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


## Cleanup coordination cross-reference (2026-10-16)

`Docs/PA_TICKETS_EXPORT_CLEANUP_DESIGN.md` maps expiry, deletion, retry, and cleanup-failure events to the unresolved audit actor and transaction contract.
