# PA_TICKETS Stage 2, Slice 12 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## ExecuteAsync audit

`ExecuteAsync` loads the ticket and work item, performs assignment
authorization for the requested executor, and then delegates the mutation to
`TicketLifecycleService.Mutate`. `Ticket.StartExecution` assigns the work item,
records the executor, changes the ticket to `IN_PROGRESS`, and emits the
work-item-started domain event. Before this slice, `Mutate` invoked that action
before checking the configured transition policy. A denied `ROUTED ->
IN_PROGRESS` policy could therefore mutate the tracked ticket and work item and
append an event even though `SaveAsync` was never reached.

The method now passes `TicketStatus.InProgress` to the existing pre-mutation
policy branch. Authorization remains first; the domain invariant and mutation
still run only after the configured target transition is accepted. Successful
execution uses the same action, persistence, and response path as before.

## Regression coverage

`Denied_execution_transition_preserves_assignment_state_events_and_persistence`
uses an authorized executor with the configured `ROUTED -> IN_PROGRESS`
transition denied. It verifies that the ticket remains `Routed`, the current
assignee and last executor remain null, the work item remains unassigned and
`PENDING`, the original uncommitted event collection is unchanged, and the
repository save count remains zero.

The test helper now permits an explicit configured-transition result for this
case while retaining deterministic identity generation. No production
authorization semantics were changed.

## Files changed for this slice

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — supplies
  the known `IN_PROGRESS` target to the existing pre-mutation policy check in
  `ExecuteAsync`.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — adds denied-execution preservation assertions and the minimal test-helper
  configuration needed to exercise the denied policy.
- `Docs/PA_TICKETS_STAGE_2_SLICE_12_RESULT.md` — records this slice's evidence.

## Validation

- Focused lifecycle tests:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -v minimal`
  — **30 passed, 0 failed, 0 skipped**.
- Broader lifecycle/transaction selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **50 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed. Git emitted only existing LF/CRLF conversion
  notices for dirty files.

No database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining gaps

`WaitAsync`, `ResumeAsync`, and `ResolveAsync` still use the generic
post-mutation transition-policy branch and require a separate source-backed
decision about their target statuses and preflight semantics. Provider
persistence, rollback/commit outcomes, audit-chain durability, concurrency,
and API acceptance remain unverified and require the existing TGT/OP/BASE
approvals plus resolution of the RBAC metadata contract. Stage 2 therefore
remains `PARTIAL`.

All pre-existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
