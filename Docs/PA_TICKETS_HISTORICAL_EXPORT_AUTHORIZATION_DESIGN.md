# PA_TICKETS — Historical Export Authorization Design

**Review date:** 2026-10-16  
**Review type:** source and documentation inspection only  
**Acceptance baseline:** 8 PASS / 3 PARTIAL / 13 BLOCKED

## Confirmed architecture and data flow

1. API export routes call IReportExportService. The concrete implementation is src/PA.Tickets.Infrastructure/Reporting/ReportExportService.cs.
2. CreateAsync obtains a ReportDefinition from ReportingQueryService.GetDefinition, checks definition.Permission through IAuthorizationService.EnsurePermissionAsync, persists a ReportExportRow, and repeatedly calls IReportingQueryService.QueryAsync with the requester identity and requested filters.
3. ReportingQueryService.QueryAsync validates filters, rechecks the report permission, then AuthorizedTickets requires an active user and applies the current organization-wide authorization rule or current department/team/client/assignee predicates. Report definitions currently use TICKET_READ and enumerate supported filters and output columns in source.
4. CreateAsync formats the complete result through IReportExportFormatter and stores bytes through IFileStorage.PutAsync. LocalFileStorage publishes a temporary file by atomic move and records size/hash/storage key.
5. ReportExportRow is defined in src/PA.Tickets.Infrastructure/Execution/ReportExportRows.cs. It stores report code, format, requester, serialized filters, lifecycle timestamps/status, row count, file metadata, failure information, correlation ID, and expiry.
6. GetAsync and DownloadAsync perform a non-disclosing owner lookup and recheck the current report permission. DownloadAsync denies expired/non-completed/missing artifacts and then fails closed because the row cannot prove historical row authorization.
7. The current API therefore expects retrieval of the generated artifact, but the implementation intentionally does not return an artifact where current authorization cannot establish its safety.

Confirmed versus unavailable evidence:

- Confirmed: current permission and current row scope are applied while generating each page.
- Confirmed: filters and report code are retained, but no authorization snapshot or report-definition version is retained.
- Confirmed: current report definitions and field columns are source-defined and can change between requests if code/configuration changes.
- Not established: deployed schema, provider transaction semantics, storage service behavior beyond LocalFileStorage, or whether external clients rely on byte-for-byte snapshot semantics.

## Existing metadata and evidentiary limits

The row can prove which requester asked for which report code, format, serialized filters, time, correlation, and resulting file hash. It cannot prove:

- the exact ticket/row identifiers included;
- the effective membership, role, permission, organization, client, department, team, or assignee scope at generation;
- effective-from/effective-to or grant version used for each row;
- report-definition or field-policy version;
- that the current scope still covers every historical row;
- that a hash or signature represents an authorized result.

A hash or signature would establish integrity of the recorded context or bytes, not continuing authorization of the data. Current TICKET_READ alone is therefore insufficient.

## Strategy A — regenerate under current authorization

At download, the service would parse the retained filters, resolve the current definition, run QueryAsync as the requester, and format a new response under current permission, active-account, and scope rules.

Advantages:

- Re-evaluates each row and field at retrieval time.
- Handles revocation, scope reduction, deactivation, changed definitions, and field policy changes without trusting old bytes.
- May avoid a new persistence table or migration.

Risks and compatibility effects:

- The result can differ from the originally generated artifact because rows, definitions, ordering, labels, fields, and permissions may have changed.
- Repeated downloads need not be byte-identical.
- Large reports repeat database work and may exceed request timeouts; streaming or a new asynchronous regeneration contract may be required.
- Replacing or bypassing existing storage changes the current download semantics and requires deterministic handling of changed row counts and file-size limits.
- Existing historical rows with incomplete or malformed filters cannot safely regenerate and must remain denied.
- A regeneration response must preserve the established format and non-disclosure behavior.

The existence of QueryAsync does not by itself prove that regeneration is compatible with the current API or operational limits.

## Strategy B — authorization-context snapshot

At creation, persist an approved, tamper-evident context bound to the artifact. Candidate evidence includes requester identity, active-account state, report code and definition/field-policy fingerprint, normalized filters, applicable client/department/team/assignee/resource/organization scope, effective permission/grant version, artifact hash, and generation timestamp. A snapshot should also define explicit invalidation for permission revocation, scope reduction, deactivation, report-definition changes, and field-policy changes.

A context fingerprint proves that the context was not altered. It does not prove row-level authorization unless the retained evidence is sufficient to reconstruct or verify the exact rows and fields included. A minimal scope-only snapshot may still be insufficient for reports whose rows change independently of scope. Exact row IDs or a verifiable row-set/authorization proof may be required, subject to owner and data-retention approval.

Advantages:

- Preserves generated snapshot semantics and efficient downloads.
- Makes revocation and scope decisions explicit and testable.
- Can support audit and incident reconstruction.

Costs and blockers:

- Requires an approved schema or durable sidecar, migration and rollback plan, retention policy, and provider transaction review.
- Increases storage and generation work.
- Requires a version/invalidation strategy that current source does not contain.
- Legacy rows cannot be retroactively made provable; they remain denied unless an owner-approved compatibility rule exists.

## Decision matrix

| Criterion | Regenerate at download | Authorization-context snapshot |
|---|---|---|
| Security after revocation/scope reduction | Strong current-state check; safest if query path remains complete | Strong only if snapshot proves included rows/fields and invalidation is authoritative |
| Existing API compatibility | Changes artifact snapshot and repeat-download behavior | Preserves current generated-artifact contract |
| Report correctness | Current data/definition, potentially different from original | Original generated result, if evidence is sufficient |
| Performance | Repeats report query and formatting per download | Fast download; extra creation/storage cost |
| Storage | Can reuse current row metadata | Requires new persisted context/evidence |
| Implementation complexity | Service/API and timeout/streaming changes | Schema, migration, versioning, invalidation, and transaction design |
| Provider impact | Possibly none, but runtime behavior still needs testing | Explicit provider/schema work required |
| Revocation and scope changes | Naturally re-evaluated | Requires explicit invalidation or proof |
| Testability | Provider-independent with deterministic query fakes | Requires tamper, version, and persistence tests plus provider tests |
| Operational reliability | Sensitive to query availability at download | Sensitive to context durability and invalidation correctness |

## Recommendation and owner boundary

Neither strategy is safe to implement silently. The smallest defensible immediate behavior is to keep downloads fail-closed whenever current authorization cannot establish artifact safety.

For owner review, Strategy A is the smaller persistence change but changes the API’s generated-artifact semantics and may be impractical for large exports. Strategy B better preserves the existing contract but requires an approved authorization-context schema and invalidation model. The owner must choose the product behavior and approve any schema/provider work.

No new permission, role grant, scope, or schema field is proposed as approved by this document.

## Required owner decisions

- Is a download required to return the original generated bytes, or may it regenerate under current authorization?
- If regeneration is chosen, what latency, row-limit, timeout, and repeated-download guarantees apply?
- If a snapshot is chosen, what exact evidence proves every row and field, and how are definition/permission/scope versions invalidated?
- How are legacy artifacts with insufficient metadata handled? Current fail-closed denial is the safe default.
- What are the rules for deactivation/reactivation, permission revoke/regrant, scope reduction/expansion, and changed report definitions?
- Are sensitive exports subject to independent approval? No distinct export permission is assumed.
- What audit event and failure contract applies to authorization decisions?
- Which provider/schema owner approves migrations, backup, rollback, and deployed-baseline evidence?

## Minimal implementation plan after approval

1. Freeze the chosen contract and acceptance criteria.
2. Add provider-independent tests with fake authorization, current scope, report definitions, clock, storage, and query results.
3. Implement the selected strategy while retaining fail-closed behavior for unsupported legacy artifacts.
4. Add tamper/invalidation and concurrency tests appropriate to the strategy.
5. Prepare schema/migration and provider tests only under the existing database approval gate.
6. Run API and browser E2E separately; do not treat mocked or provider-independent tests as database verification.

## Regression test plan

Provider-independent tests:

- unchanged active permission and scope permits the approved behavior;
- permission revoke/regrant, scope reduction/expansion, deactivation/reactivation;
- report-definition and field-permission changes;
- changed filters and repeated downloads;
- expired, failed, incomplete, missing, corrupt, and partially deleted artifacts;
- tampered or inconsistent authorization metadata remains denied;
- non-owner and unknown identifiers remain non-disclosing;
- concurrent download and expiry/cleanup behavior;
- established file format, response metadata, row limits, and size limits.

Provider-dependent tests, deferred until approved:

- persistence of context/evidence and rollback;
- transaction atomicity between export status, artifact metadata, and audit;
- provider SQL translation for regeneration and scope predicates;
- locking/lease behavior and storage durability;
- migration compatibility with existing export rows and deployed baselines.

## Validation limitations

Only checked-in source and documents were inspected. No database, provider, migration, storage service, or database-backed test was accessed. No application behavior or schema changed. Existing downloads remain fail-closed wherever current authorization cannot be established. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.


## API and test anchors

- `src/PA.Tickets.Api/Controllers/ReportExportsController.cs` exposes authenticated `POST api/v1/reports/{reportCode}/exports`, `GET api/v1/exports/{exportId}`, and `GET api/v1/exports/{exportId}/download`; the controller maps the authenticated claim to the actor passed to the service.
- `src/PA.Tickets.Api/Controllers/ReportsController.cs` exposes the source report catalogue, definition, and query endpoints. These use the same actor identity and `ReportingQueryService` authorization path.
- Existing focused tests are `tests/PA.Tickets.Domain.Tests/ReportExportAuthorizationTests.cs` and `ReportExportFormatterTests.cs`. They are provider-independent evidence for the existing safe subset, not proof of persistence, provider, or historical-artifact authorization.


## Strategy selection review — 2026-10-16

### Exact current download contract

ReportExportsController exposes authenticated GET api/v1/exports/{exportId}/download. The controller calls DownloadAsync with the actor from NameIdentifier/sub and returns ASP.NET Core File(result.Content, result.Status.ContentType ?? application/octet-stream, result.FileName, enableRangeProcessing: false).

The current generated artifact contract is:

- CSV uses <report-code>.csv, text/csv; charset=utf-8, UTF-8 BOM, definition-controlled columns, deterministic formatter ordering, and CSV formula neutralization.
- XLSX uses <report-code>.xlsx and the OpenXML spreadsheet content type.
- PDF is rejected as unsupported.
- Creation accepts serialized ReportQueryRequest filters and MaxRows up to 10,000; query pages are size 100. ReportQueryService applies requested sort, paging, and current authorization predicates.
- Storage uses a generated key under exports/{export-id}/{random}.{format}. The original file name is retained in storage metadata but is not persisted separately on ReportExportRow; DownloadAsync currently never reaches a successful file response.
- Repeated downloads are owner-only and current-permission checked, but currently fail closed after those checks because historical row authorization is unproven. No byte-for-byte repeated-download guarantee is established by source.

### Regeneration decision

The existing QueryAsync path can technically receive the current actor and retained filters, but source does not establish that regeneration reproduces the original artifact. Current rows, definitions, columns, field policies, labels, ordering, totals, and scope membership may differ. Regeneration also repeats database work, may exceed request limits for large exports, and changes the current API contract from retrieving stored bytes to producing a new result. Existing malformed or incomplete filters cannot be safely regenerated.

### Snapshot decision

A snapshot can preserve original bytes only if it records enough evidence to verify every included row and field, plus definition/field-policy and effective scope versions. Requester, filters, report code, timestamps, hash, and a signed context fingerprint alone prove provenance/integrity, not continuing row-level authorization. Invalidation must cover permission revocation, scope reduction, account deactivation, definition changes, and field-policy changes; current source has no durable version or invalidation model.

### Decision matrix

| Criterion | A: regenerate | B: approved snapshot |
|---|---|---|
| Current-state security | Rechecks current authorization and rows | Depends on complete row-level evidence and authoritative invalidation |
| Existing API compatibility | Changes original-artifact and repeat-download semantics | Preserves generated-file semantics |
| Correctness | Reflects current data/definition, possibly different output | Preserves original output if evidence is sufficient |
| Performance | Repeats query/format work on every download; large reports risk timeouts | Fast retrieval; extra creation/storage cost |
| Complexity | Service/API contract and streaming/timeout handling | Persistence, versioning, migration, invalidation, and concurrency design |
| Provider impact | Potentially lower, but still requires provider testing | Explicit schema/provider work required |
| Testability | Deterministic query fakes can cover policy | Requires tamper, persistence, invalidation, and provider tests |
| Legacy artifacts | Unsupported rows remain denied | Unsupported rows remain denied unless separately reconciled |

### Recommendation

Neither strategy is defensible for immediate implementation with the current architecture. The recommended operational decision is to keep downloads fail-closed until the owner approves the missing contract.

For eventual design review, Strategy B is the compatibility-preserving direction only if an approved context can actually prove row-level authorization; Strategy A is the smaller persistence change only if product owners accept changed snapshot and repeated-download semantics. This is a recommendation for review, not an approved implementation choice.

Minimum missing contract:

1. Owner decision on original bytes versus current-regenerated output.
2. Exact row/field authorization evidence or regeneration guarantees.
3. Invalidation/version semantics for permission, scope, account, definition, and field-policy changes.
4. Limits and timeout behavior for regeneration, or schema/provider approval for snapshots.
5. Audit, concurrency, and legacy-artifact behavior.

No permission, role grant, scope, schema field, or provider guarantee is introduced.

### Approval status

- [ ] Product owner approves original-artifact or regenerated-output semantics.
- [ ] Security/data owner approves row-level evidence and invalidation rules.
- [ ] Operations owner approves large-report and repeated-download behavior.
- [ ] Schema/provider owner approves any snapshot persistence and migration.
- [ ] Test owner approves provider-independent and later provider-backed acceptance criteria.

This section does not change application behavior. Existing downloads remain fail-closed.

