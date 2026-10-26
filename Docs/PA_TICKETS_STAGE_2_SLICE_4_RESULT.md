# PA_TICKETS Stage 2, Slice 4 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Bounded lifecycle audit

The status-changing entry points were traced through authorization, policy
checks, domain mutation, event recording, and repository persistence:

| Entry point | Current ordering/evidence |
|---|---|
| `ClassifyAsync` | Authorization, `NEW -> CLASSIFIED` policy preflight, domain mutation, save. Fixed in Slice 1. |
| `RequestApprovalAsync` | Authorization, `NEW -> PENDING_APPROVAL` preflight, domain approval invariant/mutation, save. Fixed in Slice 2. |
| `ApproveAsync` | Authorization, `PENDING_APPROVAL -> ROUTED` preflight, approval invariant/mutation, save. Fixed in Slice 3. |
| `CancelAsync` | Authorization, cancellation preflight, domain mutation, save. Fixed in this slice. |
| `TransitionSpecializedStatusAsync` | Authorization, specialized domain preflight and configured policy preflight, mutation, save. Existing coverage retained. |
| `RouteAsync`, `ExecuteAsync`, `WaitAsync`, `ResumeAsync`, `ResolveAsync`, `ReopenAsync`, `CloseAsync` | Use the generic mutation path; status policy is still evaluated after the action when no target status is supplied. These are remaining candidates for individually scoped review. |
| `IncidentService` resolve/reopen/close | Specialized preflight is performed before subtype mutation; lifecycle transition is then executed inside the service transaction. |
| `ProblemService` resolve/close/reopen | Specialized preflight is performed before subtype mutation; lifecycle transition and audit writes are inside the service transaction. |

The four focused regressions now prove that denied classification, approval
request, approval completion, and cancellation preserve status/fields/events
and make no repository save. No additional transition correction was bundled
into this slice.

## Slice correction

`CancelAsync` now supplies `TicketStatus.Cancelled` to the existing
pre-mutation policy branch. The domain cancellation rule, authorization, API
contract, and successful path are unchanged.

## Next integrity gap selected

The higher-value issue is transaction ownership in the non-lifecycle
`IncidentService` mutation paths. `AcknowledgeAsync`, `AssignCommanderAsync`,
`DeclareMajorAsync`, `RestoreAsync`, `UpdateImpactAsync`, and `UpdateUrgencyAsync`
call `AddAuditAsync` before their final `db.SaveChangesAsync` without an outer
transaction. `AddAuditAsync` calls `AuditTransaction.BeginIfNeededAsync`; when
no transaction exists it starts and commits an audit transaction independently.
Consequently, an audit row can commit before the incident mutation/event write,
allowing an audit-only success if the later persistence fails. The same shape
exists in incident creation.

This is a concrete source-level atomicity risk, but proving rollback, commit
failure, and connection-loss behavior requires PostgreSQL/Npgsql evidence. A
safe follow-up should introduce one shared, infrastructure-owned transaction
boundary for these paths and provider-independent tests around its ownership,
then obtain separately authorized provider validation. No such runtime seam was
introduced here.

## Validation

- Focused and broader lifecycle/transaction selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **44 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for dirty files.

No database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining blockers

Stage 2 still lacks provider-backed persistence, rollback/commit outcome,
audit-chain durability, concurrency, and lifecycle API acceptance evidence.
The approved PostgreSQL authorization catalogue still lacks `S_MODULE_CODE`,
so authorization remains fail-closed; no schema or RBAC workaround was used.
All owner/provider approvals and the separate migration-026 chain remain
unchanged.

All pre-existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
