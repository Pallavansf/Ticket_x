# PA_TICKETS Stage 2, Slice 10 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Remaining-method audit

| Method | Target status | Result |
|---|---|---|
| `RouteAsync` | `ROUTED` | Still uses generic post-mutation policy path; remains a candidate. |
| `ExecuteAsync` | `IN_PROGRESS` | Assignment authorization precedes mutation; status policy remains post-mutation. |
| `WaitAsync` | `WAITING` | Waiting-reason checks precede mutation; status policy remains post-mutation. |
| `ResumeAsync` | `IN_PROGRESS` | Domain resume mutation/event precedes generic status policy. |
| `ResolveAsync` | `RESOLVED` | Domain resolution invariants run during mutation; configured policy remains post-mutation. |
| `ReopenAsync` | `REOPENED` | **Fixed:** target-status preflight now runs before clearing resolved/closed fields and recording the event. |
| `CloseAsync` | `CLOSED` | Fixed in Slice 9. |

The Slice 1–5 corrections for classification, approval request, approval
completion, cancellation, and close remain covered by the same regression
suite. Specialized Incident/Problem preflight remains separate and unchanged.

## Correction and regression

`ReopenAsync` now passes `TicketStatus.Reopened` to the existing pre-mutation
policy branch. The new regression starts from a resolved ticket, denies the
configured transition, and verifies status, `ResolvedAt`, `ClosedAt`, domain
events, and repository save count remain unchanged.

No authorization rule, domain invariant, API contract, or successful-operation
behavior changed.

## Validation

- Focused lifecycle/transaction selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **48 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for dirty files.

No database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining gaps

`RouteAsync`, `ExecuteAsync`, `WaitAsync`, `ResumeAsync`, and `ResolveAsync`
still require individually bounded preflight corrections if their target
statuses and domain setup can be covered without broad refactoring. Provider
rollback, commit ambiguity, audit durability, concurrency, and API acceptance
remain blocked by the pending TGT/OP/BASE approvals and RBAC schema contract.

All existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
