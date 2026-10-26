# PA_TICKETS Stage 2, Slice 3 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Confirmed gap

The remaining lifecycle review found that `ApproveAsync` used the generic
mutation path without a known target-status preflight. `Ticket.Approve` changed
`PENDING_APPROVAL` to `ROUTED`, set the approval flag, and recorded an event
before `TicketLifecycleService.Mutate` consulted the configured transition
policy. A denied policy therefore left the tracked aggregate changed even
though persistence was not called.

## Correction

`ApproveAsync` now passes `TicketStatus.Routed` to the existing pre-mutation
policy branch. Authorization remains first; the domain approval invariants in
`Ticket.Approve` still run before mutation; and the repository is called only
after the checks and mutation succeed. No transition rule or public contract
was changed.

## Regression test

The focused test starts with a valid pending approval, denies the configured
`PENDING_APPROVAL -> ROUTED` transition, and verifies that status, approval
required/approved flags, approver identity, domain events, and repository save
count are unchanged.

## Files changed for this slice

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — supply the
  known approval-completion target status.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — add denied approval-completion preservation coverage.
- `Docs/PA_TICKETS_STAGE_2_SLICE_3_RESULT.md` — this evidence report.

Earlier changes in the first two files were pre-existing and were preserved.

## Validation

- Focused lifecycle tests:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -v minimal`
  — **25 passed, 0 failed, 0 skipped**.
- Broader lifecycle/transaction regression selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **43 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for dirty files.

One initial parallel test/build attempt encountered a transient compiler file
lock; sequential reruns completed successfully and are the reported results.

No PostgreSQL or Oracle connection, SQL, migration, DDL/DML, schema/RBAC
change, privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining gaps and next slice

Stage 2 remains `PARTIAL`. Other status-changing entry points still require
individual source-backed review. Provider evidence remains required for
persistence, rollback/commit outcomes, audit durability, concurrency, and API
acceptance. The `S_MODULE_CODE` authorization-catalogue mismatch remains a
fail-closed schema/RBAC blocker.

The next slice should review one additional confirmed status-changing entry
point, or move to transaction-outcome/audit durability coverage if no further
pre-mutation defect is found.

All tracked, untracked, ignored, and local worktree changes were preserved. No
reset, clean, stash, branch switch, commit, or push occurred.
