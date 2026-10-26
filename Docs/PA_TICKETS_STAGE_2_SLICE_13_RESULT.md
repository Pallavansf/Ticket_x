# PA_TICKETS Stage 2, Slice 13 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## WaitAsync audit

`WaitAsync` resolves the waiting-reason definition and validates its allowed
source status and required comment, evidence, and expected-date fields before
calling the shared mutation path. `Ticket.Wait` then changes the work item to
`WAITING`, stores waiting metadata, changes the ticket from `IN_PROGRESS` to
`WAITING`, and records `WORK_ITEM_WAITING`. Before this slice, the configured
transition policy was checked only after that action, so a denied
`IN_PROGRESS -> WAITING` policy could leave waiting state and a domain event on
the tracked aggregate while `SaveAsync` was never called.

The service now passes `TicketStatus.Waiting` to the existing pre-mutation
target-status branch. Authorization still occurs in `Mutate` after the existing
waiting-reason request validation and before policy evaluation and mutation.
Successful waiting behavior, reason validation, domain invariants, and
persistence ordering are unchanged.

## Regression coverage

`Denied_waiting_transition_preserves_waiting_state_events_and_persistence`
creates a routed ticket, starts its work item, resolves a valid waiting reason,
and denies the configured transition. It verifies that the ticket remains
`InProgress`, the work item remains `IN_PROGRESS`, waiting reason/comment/start
metadata remain unset, the original uncommitted events are unchanged, and the
repository save count is zero.

## Files changed for this slice

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — supplies
  the known `WAITING` target to the existing pre-mutation policy check.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — adds denied-waiting preservation coverage and a minimal waiting-reason
  test catalog.
- `Docs/PA_TICKETS_STAGE_2_SLICE_13_RESULT.md` — records this slice's evidence.

## Validation

- Focused lifecycle tests:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -v minimal`
  — **31 passed, 0 failed, 0 skipped**.
- Broader lifecycle/transaction selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **51 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed. Git emitted only existing LF/CRLF conversion
  notices for dirty files.

No database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining gaps

`ResumeAsync` and `ResolveAsync` remain on the generic post-mutation transition
policy path and require separate source-backed review of their target statuses
and preflight semantics. Provider persistence, rollback/commit outcomes,
audit-chain durability, concurrency, and API acceptance remain unverified and
require the existing TGT/OP/BASE approvals plus resolution of the RBAC metadata
contract. Stage 2 therefore remains `PARTIAL`.

All pre-existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
