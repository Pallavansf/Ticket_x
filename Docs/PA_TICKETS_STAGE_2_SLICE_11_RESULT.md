# PA_TICKETS Stage 2, Slice 11 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## RouteAsync audit

`RouteAsync` first loads the ticket and performs assignment authorization, then
allocates a work-item ID, and finally calls the generic mutation path. Before
this slice, `Ticket.Route` changed status to `ROUTED`, added a work item, and
recorded `WORK_ITEM_CREATED` before the configured transition policy ran.
Therefore a denied `CLASSIFIED -> ROUTED` policy could leave routing state and
an event on the tracked aggregate despite zero persistence calls.

The method now supplies `TicketStatus.Routed` to the existing pre-mutation
policy check. Authorization remains first, the domain route invariant still
runs in `Ticket.Route`, and work-item allocation remains before the mutation
callback. A denied route is rejected before aggregate mutation.

## Regression coverage

The new test uses a classified ticket and denies the configured transition. It
asserts that status remains `Classified`, department/team routing fields remain
null, no work item is added, the original domain events remain unchanged, and
the repository save count is zero.

## Files changed

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — pass the
  known route target to the pre-mutation policy branch.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — add denied-route preservation coverage and a deterministic identity fake.
- `Docs/PA_TICKETS_STAGE_2_SLICE_11_RESULT.md` — this report.

## Validation

- Focused lifecycle tests:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -v minimal`
  — **29 passed, 0 failed, 0 skipped**.
- Broader lifecycle/transaction selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **49 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for dirty files.

No database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining gaps

`ExecuteAsync`, `WaitAsync`, `ResumeAsync`, and `ResolveAsync` remain for
separate source-backed review. Provider persistence, rollback/commit outcome,
audit-chain durability, concurrency, and API acceptance remain unverified and
blocked by pending TGT/OP/BASE approvals and the unresolved RBAC metadata
contract. Stage 2 remains `PARTIAL`.

All existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
