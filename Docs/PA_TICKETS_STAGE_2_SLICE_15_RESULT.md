# PA_TICKETS Stage 2, Slice 15 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## ResolveAsync audit

`ResolveAsync` delegates to the shared mutation path, and `Ticket.Resolve`
requires an executing ticket, recorded evidence, and every work item in the
`COMPLETED` state. It then changes the ticket to `RESOLVED`, sets
`ResolvedAt`, and records `TICKET_RESOLVED`. Before this slice, the configured
transition policy ran only after that mutation. A denied `IN_PROGRESS ->
RESOLVED` policy could therefore leave the tracked ticket resolved and append
an event even though `SaveAsync` was never reached.

The method now supplies `TicketStatus.Resolved` to the existing pre-mutation
target-status branch. Authorization and the aggregate's resolution invariants
remain in their prior order, and successful resolution uses the same mutation,
persistence, and response path.

## Regression coverage

`Denied_resolution_transition_preserves_resolution_state_events_and_persistence`
creates a routed ticket, starts execution, records evidence, completes its work
item, and denies the configured transition. It verifies that the ticket remains
`InProgress`, `ResolvedAt` remains null, the completed work-item state is
unchanged, the original uncommitted events are unchanged, and repository save
count is zero.

## Files changed for this slice

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — supplies
  the known `RESOLVED` target to `ResolveAsync` preflight.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — adds denied-resolution preservation coverage.
- `Docs/PA_TICKETS_STAGE_2_SLICE_15_RESULT.md` — records this slice's evidence.

## Validation

- Focused lifecycle tests:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -v minimal`
  — **33 passed, 0 failed, 0 skipped**.
- Broader lifecycle/transaction selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **53 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed. Git emitted only existing LF/CRLF conversion
  notices for dirty files.

No database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining gaps

The bounded generic lifecycle pre-mutation review is now complete for the
methods identified in the preceding slices. Provider persistence, rollback and
commit outcomes, audit-chain durability, concurrency, and API acceptance remain
unverified and require the existing TGT/OP/BASE approvals plus resolution of the
RBAC metadata contract. Stage 2 therefore remains `PARTIAL`.

All pre-existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
