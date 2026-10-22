# PA_TICKETS export lifecycle authorization decisions

Decision status: **recommended policy for owner approval; not implemented**. Review date: 2026-10-16. This document is based on static source and documentation inspection. No database/provider access occurred.

## Current implementation

`ReportExportsController` exposes:

- `POST /api/v1/reports/{reportCode}/exports` → `ReportExportService.CreateAsync`.
- `GET /api/v1/exports/{exportId}` → `GetAsync`.
- `GET /api/v1/exports/{exportId}/download` → `DownloadAsync`.

All three routes have `[Authorize]`; the actor is taken from the authenticated `ClaimTypes.NameIdentifier` or `sub` claim. Creation resolves the report definition, checks its current permission (`TICKET_READ` today), persists the requester, report code, serialized filters, correlation ID, status, expiry, and storage metadata, then regenerates rows through `ReportingQueryService.QueryAsync`. That query applies the current authorized ticket population and explicit organization-scope rule before generation.

`GetAsync` and `DownloadAsync` locate the row with `Id == exportId && RequestedBy == actor`. They do not re-evaluate the report permission, current client/department/team/assignee scope, organization scope, or user active state. `ExpireIfNeeded` changes a completed row to `EXPIRED` and deletes its storage object when the request observes expiry. Creation records `FAILED` on generation/storage errors and does not publish partial content. No export-specific audit-chain events are currently written. Storage keys, content type, size, SHA-256, correlation ID, report code, requester, filters, status, and expiry are retained; there is no persisted authorization snapshot proving every row in the artifact remained authorized at retrieval time.

`Docs/EXPORT_AND_SCHEDULED_REPORTING.md` documents authenticated owner restriction, report permission and scope re-evaluation while report rows are queried, seven-day retention, local storage limitations, and durable terminal states. It does not settle retrieval revocation, inactive-user behavior, export audit events, or sensitive-export approval.

## Recommended policy for owner approval

The following is the recommended security baseline for review. It is not an approved implementation contract until the checklist below is accepted.

| Decision area | Current behavior | Security risk | Recommended default | Compatibility impact | Required owner |
|---|---|---|---|---|---|
| Creation | Current report permission and current authorized query scope are checked | Correctly limits generated rows; no durable proof of row-level authorization | Keep current permission and authorized report scope at creation | Preserves `TICKET_READ`; no new permission | Security + product |
| Status | Authenticated requester ownership only | Revoked/deactivated user can still observe artifact metadata; expired reads can trigger writes/deletion | Require requester identity, active account, current applicable report permission, and current scope; non-owner returns not-found | Existing owners may lose access after revocation/deactivation | Security + product |
| Download | Authenticated requester ownership only | Artifact may contain data no longer queryable after scope reduction or permission revocation | Require requester identity, active account, current permission, and current applicable scope; deny if current authorization cannot establish artifact coverage | Existing artifacts become subject to revocation | Security + data governance |
| Scope reduction | No current scope recheck | User may download newly unauthorized data | Fail closed; current request must not authorize an artifact whose retained context cannot establish all rows remain allowed | May deny older exports after organizational changes | Security + data owner |
| Scope expansion/regrant | Ownership remains sufficient today | Regrant could unintentionally restore access without explicit policy | Regrant may authorize only if the approved contract’s current permission/scope check covers the artifact; it must not bypass expiry or ownership | Regrant behavior becomes deterministic | Security |
| Non-owner | Row query returns not-found | Low disclosure; preserve this behavior | Keep non-disclosing not-found for all status/download denial paths | Compatible with current concealment | Security |
| User deactivation/reactivation | Ownership check ignores active state | Inactive account retains artifact access; reactivation semantics undefined | Deactivation blocks immediately; reactivation requires the same current checks and never bypasses expiry | Access may change without artifact mutation | Security + product |
| Expiry | Status/download can mark row expired and delete storage during read | Read endpoint causes state changes and deletion races | Expiry blocks access immediately; physical cleanup runs separately from ordinary reads | Requires cleanup worker/job or operator procedure | Operations + data retention |
| Audit | No export audit-chain events | No reliable evidence of creation, denial, download, expiry, deletion, or failure | Audit creation, failure, download, denial, expiry, and deletion with actor, export ID, report code, correlation ID, reason, and outcome | Adds audit volume and failure coupling | Security + audit owner |
| Distinct export permission | None; creation uses report permission | A new permission could break compatibility or create unreviewed grants | Do not add a distinct export permission unless explicitly approved | Preserves current catalogue | Security + product |
| Sensitive-export approval | No independent approval | High-sensitivity exports may evade two-person review | Keep as a separate risk-based owner decision; do not infer approval from `AUDIT_EXPORT` | Possible later workflow and UI changes | Security + data owner |
| Retention | Seven days | May be too long or too short for policy | Keep seven days provisional pending owner confirmation | No immediate behavior change | Data retention + operations |

## Policy alternatives

**A. Owner-bound artifact.** The requester retrieves until expiry while the account remains valid. This has the smallest compatibility impact and preserves a stable user workflow, but a generated file may outlive the requester’s current query authorization. It is unsuitable where scope revocation must immediately remove access.

**B. Current-access recheck.** Retrieval additionally requires current permission, active account, and current scope. This reduces revocation risk and fits fail-closed RBAC, but may deny legitimate historical exports after organizational changes. It still cannot prove every old row is authorized unless the artifact retains sufficient authorization context or the system stores a safe authorization snapshot.

**C. Distinct export authorization.** Retrieval requires an owner-approved export capability, potentially with independent approval and separate scope rules. This provides the clearest separation for sensitive data but requires a new approved catalogue, role-ceiling analysis, grants, UI, migrations, and compatibility decisions. No permission name is proposed here.

The recommended baseline is closest to **B**, without introducing a distinct permission. The owner must approve whether artifact authorization uses a stored scope snapshot, conservative intersection of artifact filters with current scope, or another formally reviewed rule. Checking the requester’s current permission alone is insufficient evidence that every row in an old artifact is still authorized.

## Artifact metadata sufficiency

Current metadata identifies the report, requester, serialized filters, correlation ID, creation/completion/expiry times, status, storage key, hash, size, and content type. It does not reliably capture the exact authorized row set, effective role/membership, client/departments/teams/assignee scope, permission metadata version, report-definition version, or field-level authorization at generation time. Therefore current metadata is insufficient to prove that a current permission check covers every row in a historical artifact. This is a schema and data-contract decision, not something to infer or repair in this review.

## Edge-case contract questions

- Deactivation blocks status/download immediately; reactivation follows the same current checks and does not revive expired artifacts.
- Revocation blocks retrieval under the recommended baseline; regrant does not bypass expiry or ownership.
- Scope reduction blocks retrieval unless the approved artifact-context rule proves all data remains authorized; later expansion does not authorize a different owner’s artifact.
- Changed report definitions or field permissions require an explicit versioning rule. Current source stores report code but not a definition version.
- Missing, corrupt, or partially deleted files return a controlled unavailable outcome, remain non-disclosing to non-owners, and produce an audit event.
- Concurrent download and expiry/cleanup require a defined lock/lease or atomic object-read policy; ordinary status/download reads must not perform cleanup writes.
- Status requests must not unexpectedly mutate rows or delete files; expiry processing belongs to a separate cleanup path.
- Audit failure and storage failure require an owner-approved atomicity policy. Current creation persists failure state but does not write export audit events.

## Approval checklist

The following remain visibly unresolved until owners sign off:

- [ ] Security owner approves current-permission/current-scope checks for status and download.
- [ ] Product/data owner approves requester-only versus current-access retrieval semantics.
- [ ] Data owner defines how an artifact proves row-level authorization after scope reduction.
- [ ] Security owner defines inactive-user and reactivation behavior.
- [ ] Operations owner defines expiry blocking versus asynchronous physical deletion and concurrency handling.
- [ ] Audit owner approves event names, fields, retention, and failure handling for creation, failure, download, denial, expiry, and deletion.
- [ ] Security/data owner decides whether any exports require independent approval.
- [ ] Product/security owners explicitly approve or reject a distinct export permission; no name is assumed.
- [ ] Data-retention owner confirms or changes the provisional seven-day retention.
- [ ] Operations owner confirms storage durability, cleanup ownership, and missing/corrupt artifact handling.

## Safe implementation sequence after approval

1. Freeze the approved contract and artifact authorization context without changing grants.
2. Add provider-independent authorization and no-disclosure tests using deterministic fakes.
3. Implement status/download checks and separate cleanup with explicit audit events.
4. Add concurrency, storage-failure, audit-failure, expiry, revocation, and scope-reduction handling.
5. Run API and focused unit tests only; do not claim persistence or provider behavior.
6. Prepare provider-specific schema/migration work only after the existing database approval gate.
7. Run approved database and browser E2E phases separately and report their evidence boundaries.

## Required provider-independent tests

Owner/non-owner status and download; anonymous/unauthorized/forbidden mapping; permission revocation and regrant; scope reduction and expansion; deactivation/reactivation; changed report definition/field permission; expired, missing, corrupt, and partially deleted files; concurrent expiry/download; failed generation/storage; audit success/failure; non-disclosing not-found behavior; and confirmation that status/download do not perform cleanup writes.

Provider-backed transaction, locking, storage durability, SQL translation, migration, and database-to-UI E2E tests remain separate blocked work. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

## Implementation update — 2026-10-16

### Implemented

- `ReportExportService.GetAsync` now re-evaluates the current report permission after the existing non-disclosing owner lookup.
- `ReportExportService.DownloadAsync` now re-evaluates the current report permission, blocks expired/non-completed/missing-storage artifacts, and fails closed because the current row does not contain an authorization snapshot proving every generated record remains within current scope.
- Completed artifacts observed after their retention deadline are reported as `EXPIRED` without changing the execution row or deleting storage from an ordinary GET request.
- Existing export creation and report-generation logic were left unchanged.

### Partially implemented

The current authorization service call is the established source of current permission and active-principal checks, but the export service does not possess a persisted artifact-level scope snapshot. Current permission alone is therefore not treated as proof that every old row remains authorized. Scope reduction, report-definition changes, field-permission changes, and organization-scope changes fail closed for download because the artifact cannot establish coverage.

### Still blocked by contract or persistence design

- Physical cleanup requires a separately owned background cleanup path and concurrency contract; ordinary status/download requests no longer delete files.
- Export creation, failure, download, denial, expiry, deletion, and cleanup-failure audit events are not yet added. Existing audit persistence is tied to the ticket context/hash-chain conventions; wiring execution exports into it needs an approved transaction and failure policy.
- Seven-day retention remains provisional.
- Missing/corrupt/partially deleted file handling beyond the existing unavailable outcome needs provider-independent storage fakes and an approved operational policy.
- Independent approval and any distinct export permission remain owner decisions; no permission was introduced.

### Verification

Executed without database/provider setup:

```text
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --filter 'FullyQualifiedName~ReportExportAuthorizationTests|FullyQualifiedName~ReportExportFormatterTests'
```

Result: **5 passed, 0 failed**.

```text
dotnet build src/PA.Tickets.Api/PA.Tickets.Api.csproj --no-restore
```

Result: **0 warnings, 0 errors**.

No PostgreSQL tests, full test project, migrations, cleanup jobs, or provider-backed checks were run. Acceptance remains **8 PASS / 3 PARTIAL / 13 BLOCKED**.

## Remaining-gap assessment cross-reference (2026-10-16)

The source-only assessment in `Docs/PA_TICKETS_EXPORT_LIFECYCLE_REMAINING_GAPS.md` records the unresolved historical-artifact authorization, audit lifecycle, and asynchronous cleanup/concurrency gaps. The safe subset remains fail-closed for downloads; no new permission or schema decision is approved.

## Historical authorization design cross-reference (2026-10-16)

See `Docs/PA_TICKETS_HISTORICAL_EXPORT_AUTHORIZATION_DESIGN.md` for the data-flow trace, evidentiary limits, strategy matrix, owner decisions, and deferred test plan. No strategy is approved or implemented.

## Strategy selection review cross-reference (2026-10-16)

The historical design now records the exact download response contract and compares regeneration with an authorization-context snapshot. Neither is approved; downloads remain fail-closed pending the owner contract.

## Audit contract assessment cross-reference (2026-10-16)

The audit contract assessment is documented in `Docs/PA_TICKETS_EXPORT_AUDIT_CONTRACT_DESIGN.md`; event names, worker actors, failure semantics, and persistence ownership remain unresolved owner decisions.

## Cleanup design cross-reference (2026-10-16)

The cleanup assessment is documented in `Docs/PA_TICKETS_EXPORT_CLEANUP_DESIGN.md`; expiry enforcement remains separate from physical deletion and all cleanup decisions remain unresolved.
