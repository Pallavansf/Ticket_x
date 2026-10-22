# PA_TICKETS Export Evidence Schema Reconciliation

**Scope:** ticket-aging contributor evidence only  
**Review mode:** source and checked-in documentation inspection  
**Status:** proposal only; no persistence, migration, or database operation approved  
**Acceptance baseline:** 8 PASS / 3 PARTIAL / 13 BLOCKED

## Executive recommendation

The repository does not currently support durable ticket-aging evidence. ReportExportRow has no evidence state/count, ExecutionDbContext has no contributor entity or mapping, and ReportExportService has no transaction enclosing contributor persistence and export completion.

Keep the existing in-memory contributor capture and fail-closed historical downloads. Approve a two-table logical contract, then implement a provider-backed writer only after PostgreSQL and Oracle baselines are reconciled. The provider-independent ITicketAgingEvidenceWriter/TicketAgingCompletion seam is suitable for orchestration tests, but it does not prove durability or transaction atomicity.

No output, permission, scope, audit, cleanup, or download behavior should be enabled by this proposal.

## Verified source facts

- src/PA.Tickets.Infrastructure/Execution/ReportExportRows.cs defines ReportExportRow mapped to PA_TICKETS_REPORT_EXPORT.
- Existing export fields include requester, report code, serialized filters, status, lifecycle timestamps, row count, file size, content type, file hash, storage key, failure metadata, correlation ID, and expiry.
- src/PA.Tickets.Infrastructure/Execution/ExecutionDbContext.cs exposes ReportExports, but no contributor DbSet or contributor mapping exists.
- src/PA.Tickets.Infrastructure/Reporting/ReportingQueryService.cs derives TicketAgingContributor values from the same AgingProjection sequence used to build TicketAgingRow values.
- src/PA.Tickets.Infrastructure/Reporting/TicketAgingEvidence.cs validates page alignment and duplicate IDs in memory. TicketAgingCompletion.StoreAndCompleteAsync stores the artifact before invoking the internal writer.
- src/PA.Tickets.Infrastructure/Reporting/ReportExportService.cs saves the initial export row, queries/pages and formats the report, stores the artifact, then updates the export row and calls SaveChangesAsync. No explicit completion transaction is opened.
- The normal public constructor leaves the internal evidence writer null. The current DI registration in src/PA.Tickets.Infrastructure/DependencyInjection.cs resolves the normal constructor, so the seam is not durable persistence.
- DownloadAsync still fails closed after owner, permission, expiry, and completion checks because retained ticket authorization is not established.
- Focused provider-independent tests cover evidence ordering, duplicate/incomplete evidence, storage/evidence failure ordering, formatter bytes, and permission denial. They do not prove provider transactions or persistence.

## Proposed parent export contract

Add two nullable/compatibility-safe fields to PA_TICKETS_REPORT_EXPORT:

- S_EVIDENCE_STATUS
- N_EVIDENCE_COUNT

Recommended status values:

| Status | Meaning | Allowed transition |
|---|---|---|
| NOT_CAPTURED | No durable evidence exists; legacy and unsupported exports | CAPTURING only for a new supported export |
| CAPTURING | Evidence collection is in progress | COMPLETE or FAILED |
| COMPLETE | Contributor rows and completion metadata committed | Terminal for the artifact |
| FAILED | Evidence or completion failed | Terminal for that attempt; no download |

New export rows begin as CAPTURING. Existing rows and all non-ticket-aging rows are treated as NOT_CAPTURED. A legacy row must not be backfilled with fabricated evidence.

The existing N_ROW_COUNT, S_FILE_HASH, storage metadata, requester, report, filters, lifecycle timestamps, and expiry are reused. No duplicate evidence copy of those values is proposed.

## Proposed contributor contract

Add a ReportExportContributorRow mapped to a new pilot-specific table, for example PA_TICKETS_REPORT_EXPORT_CONTRIBUTOR, with:

- N_EXPORT_ID  non-null foreign key to PA_TICKETS_REPORT_EXPORT.
- N_TICKET_ID  non-null stable ticket resource ID.

Use a composite primary key or unique constraint on (N_EXPORT_ID, N_TICKET_ID). Add an index beginning with N_EXPORT_ID for authorization lookup. A separate ticket-only index is not justified yet.

No output ordinal is required for this one-ticket-per-row report. Set membership plus N_EVIDENCE_COUNT == N_ROW_COUNT is sufficient for the proposed authorization check. It does not prove artifact row ordering; that limitation must remain explicit. An ordinal would be a separate contract decision.

## Completion transaction

After all pages pass the existing alignment, count, total, and uniqueness checks:

1. Format the unchanged ReportResult.
2. Write the artifact to external storage and obtain the stored hash/metadata.
3. Begin one EF database transaction.
4. Insert all contributor rows.
5. Set parent evidence count and S_EVIDENCE_STATUS = COMPLETE.
6. Persist artifact hash, storage metadata, row count, completion timestamp, and S_STATUS = COMPLETED.
7. Commit.
8. Return success only after commit succeeds.

The external storage write is outside the database transaction. A database commit cannot make storage publication atomic. The writer must therefore leave the export non-downloadable whenever the completion transaction fails.

## Existing-row and non-ticket-aging compatibility

- Existing rows with null evidence fields are NOT_CAPTURED.
- Existing non-ticket-aging exports remain unchanged and are not assigned contributor evidence.
- Existing completed artifacts without evidence remain denied for historical download.
- No automatic grant, scope, migration mapping, or historical actor is introduced.

## PostgreSQL reconciliation and migration plan

The requested path database/migrations/postgresql/026_phase8_6_reporting.sql does not exist. The checked-in file is database/migrations/026_phase8_6_reporting.sql.

That migration creates PA_TICKETS_REPORT_EXPORT, PA_TICKETS_REPORT_SCHEDULE, and PA_TICKETS_REPORT_SCHEDULE_EXECUTION, plus sequences, keys, and owner/due indexes. The immediate successor 027_ticket_lifecycle_authority.sql is unrelated to reporting. No later checked-in migration adds evidence fields or contributor rows.

Proposed PostgreSQL migration order, without execution:

1. Reconcile the deployed migration ledger and actual export-table catalog.
2. Add nullable parent evidence fields.
3. Add the contributor table, foreign key, composite uniqueness, and export-ID index.
4. Add defaults or compatibility handling that classify existing rows as NOT_CAPTURED.
5. Update EF mappings only after the DDL is reviewed.
6. Verify rollback/adoption behavior against the actual baseline.

PostgreSQL type choices must follow the existing quoted-name and bigint/varchar conventions. No migration is executable from this proposal.

## Oracle baseline findings

database/README.md identifies database/schema/001_PA_TICKETS_SCHEMA.sql as the supplied Oracle baseline followed by ordered additive scripts in database/migrations/.

Source search found no authoritative Oracle definition for PA_TICKETS_REPORT_EXPORT, report schedules, or report export sequences in the checked-in baseline/migrations. The PostgreSQL reporting migration cannot be treated as an Oracle baseline.

Before Oracle DDL is drafted, the owner must provide or reconcile:

- Actual export table and sequence definitions.
- Numeric precision for export and ticket IDs.
- Character lengths and timestamp types.
- Existing keys, foreign keys, and indexes.
- Migration/adoption procedure for environments that already contain reporting objects.
- Rollback and Oracle implicit-DDL-commit recovery plan.

Provider compatibility is unverified.

## Failure and recovery contract

| Failure | Required behavior | Established recovery |
|---|---|---|
| Storage fails before evidence completion | Do not invoke the writer; do not report success | Existing exception path only |
| Storage succeeds, transaction fails | Do not return success; export remains unavailable | No durable orphan cleanup/retry contract exists |
| Contributor duplicate/constraint failure | Transaction rolls back; export is not completed | Provider behavior unverified |
| Database commit succeeds, API response fails | Persisted state may be complete; repeated status lookup must determine outcome | Idempotent client/retrieval behavior requires owner review |
| Orphaned storage artifact | Must remain inaccessible without COMPLETE evidence | No established cleanup/reconciliation guarantee |

The proposal does not invent retries, cleanup, leases, reconciliation jobs, or idempotency semantics. Those require separate owner approval.

## Owner approval checklist

- [ ] Approve NOT_CAPTURED, CAPTURING, COMPLETE, and FAILED semantics.
- [ ] Approve existing rows remaining denied.
- [ ] Approve set-based evidence without output ordinals and accept the ordering limitation.
- [ ] Approve parent fields and contributor table names/constraints.
- [ ] Approve the completion transaction boundary.
- [ ] Approve storage-orphan behavior and operational ownership.
- [ ] Reconcile PostgreSQL migration baseline and target fingerprint.
- [ ] Provide and approve the Oracle reporting baseline.
- [ ] Approve provider-backed transaction, constraint, rollback, and preservation tests.
- [ ] Approve resource-by-resource current authorization before restoring downloads.

## Implementation sequence and test matrix

1. Owner approves the logical contract and Oracle baseline requirement.
2. Add provider-specific EF entities/mappings only after DDL review.
3. Implement a transaction-aware evidence writer using the existing ExecutionDbContext.
4. Keep the current in-memory page validation and unchanged formatter input.
5. Add provider-independent writer-orchestration tests using the internal seam:
   - storage-before-writer ordering;
   - storage failure does not invoke writer;
   - duplicate/incomplete evidence blocks completion;
   - writer failure propagates;
   - unchanged bytes, filename, content type, filters, and ordering.
6. Add PostgreSQL and Oracle tests for constraints, transaction rollback, existing-row defaults, and migration adoption.
7. Implement resource-by-resource reauthorization for every retained ticket ID.
8. Keep historical downloads disabled until all authorization and provider gates pass.

## Release gates and blockers

Downloads cannot be enabled until every retained ticket ID is reauthorized against current account status, permission, and resource scope. A report permission check alone is insufficient.

Persistence implementation is blocked by:

- Missing parent evidence fields and contributor entity/mapping.
- Missing explicit completion transaction.
- Missing Oracle reporting baseline.
- Unresolved storage-orphan recovery contract.
- Unverified provider constraints and rollback behavior.
- Unimplemented resource-by-resource download authorization.

No database operation, migration, provider test, application behavior change, or schema change was performed. Historical downloads remain fail-closed. Acceptance remains 8 PASS / 3 PARTIAL / 13 BLOCKED.
