# PA_TICKETS Stage 2, Slice 2 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Confirmed gap

The lifecycle entry-point review found that `RequestApprovalAsync` used the
generic `TicketLifecycleService.Mutate` path without supplying its known target
status. The generic path therefore invoked `Ticket.RequestApproval` first and
consulted the configured transition policy afterward. A denied `NEW ->
PENDING_APPROVAL` policy could leave approval state and a domain event on the
tracked aggregate even though no repository save occurred.

## Correction and regression coverage

`RequestApprovalAsync` now passes `TicketStatus.PendingApproval` to the existing
pre-mutation policy branch. Authorization still runs first, the existing domain
invariants still run in `Ticket.RequestApproval`, and persistence is attempted
only after both checks and mutation succeed.

The new regression test verifies that a denied approval transition preserves:

- `TicketStatus.New`;
- `ApprovalRequired == false`, `Approved == false`, and no approver identity;
- the original uncommitted domain-event collection; and
- zero repository save calls.

No configured policy, authorization rule, public API, transaction architecture,
or successful-operation behavior was changed.

## Files changed for this slice

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — provide the
  known approval target to the existing preflight check.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — add the denied approval-state preservation regression.
- `Docs/PA_TICKETS_STAGE_2_SLICE_2_RESULT.md` — this evidence report.

The first two files contained earlier work from the dirty worktree; that work
was preserved. Only the described incremental changes belong to this slice.

## Validation

- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -v minimal`
  — **24 passed, 0 failed, 0 skipped**.
- `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for pre-existing dirty files.

No PostgreSQL or Oracle connection, SQL, migration, DDL/DML, schema/RBAC data
change, privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed. The approved PostgreSQL target was not
contacted.

## Remaining gaps

Stage 2 remains `PARTIAL`. Other generic lifecycle methods that change status
must be reviewed individually before adding target preflight, and provider
evidence is still required for persistence, rollback/commit outcomes,
audit-chain durability, concurrency, and API acceptance. The known RBAC
catalogue mismatch (`S_MODULE_CODE` absent) remains fail-closed and unresolved;
no schema or authorization workaround was applied.

The next safe slice should address one additional confirmed status-changing
entry point only after the same source-and-test evidence is established.

All existing tracked, untracked, ignored, and local changes were preserved. No
reset, clean, stash, branch switch, commit, or push occurred.
