# PA_TICKETS Stage 2 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** source-only lifecycle/workflow increment  
**Status:** `PARTIAL` (provider-backed exit evidence is still required)

## Finding and correction

The inspected workflow implementation had a concrete state-reporting defect.
`WorkflowInstance.Cancel` iterated active and waiting `WorkflowStepInstance`
objects and called `Fail(reason)`, while emitting `WORKFLOW_CANCELLED`. A
cancelled workflow therefore left its in-flight steps in `Failed` even though
the domain model defines a distinct `WorkflowStepStatus.Cancelled` value.
Consumers could consequently treat operator cancellation as execution failure.

The narrow correction adds an internal `Cancel` transition to
`WorkflowStepInstance` and uses it from `WorkflowInstance.Cancel`. Completed or
already-cancelled steps remain protected; the workflow cancellation event and
reason are preserved. No persistence, authorization, transaction, or provider
behavior was changed.

## Source inspected

- `Docs/PA_TICKETS_MASTER_ROADMAP.md`
- `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md`
- `Docs/PA_TICKETS_STAGE_1_TRACEABILITY_RESULT.md`
- `src/PA.Tickets.Domain/Tickets/Ticket.cs`
- `src/PA.Tickets.Domain/Tickets/WorkItem.cs`
- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs`
- `src/PA.Tickets.Application/Tickets/TicketStatusTransitionPolicy.cs`
- `src/PA.Tickets.Domain/Workflow/WorkflowModel.cs`
- `src/PA.Tickets.Domain/Workflow/WorkflowVersion.cs`
- `src/PA.Tickets.Domain/Workflow/WorkflowInstance.cs`
- lifecycle and workflow tests under `tests/PA.Tickets.Domain.Tests`

The review also confirmed that durable lifecycle persistence, provider
translation, concurrency, audit-chain recovery, and API/browser acceptance are
not established by these source-level tests.

## Implementation and regression test

Files changed for this increment:

- `src/PA.Tickets.Domain/Workflow/WorkflowInstance.cs`
- `tests/PA.Tickets.Domain.Tests/UnitTest1.cs`

The focused regression test constructs a one-step workflow, cancels it, and
asserts the active step is `Cancelled`, no step-failure event is emitted, and a
workflow-cancellation event is recorded. The test would fail against the
pre-change `Fail` call.

## Validation

Executed with isolated output and no restore:

```text
dotnet build tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\stage2\bin\ -v minimal
```

Result: build succeeded, 0 warnings, 0 errors.

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~Cancelling_workflow_marks_active_steps_cancelled_instead_of_failed" -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\stage2\bin\ -v minimal
```

Result: 1 passed, 0 failed, 0 skipped.

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~WorkflowApprovalSlaTests" -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\stage2\bin\ -v minimal
```

Result: 5 passed, 0 failed, 0 skipped. This filter contains only the
database-independent workflow/approval/SLA unit tests; provider reachability
tests were excluded.

No database connection, SQL, migration, provider-backed test, provisioning,
seed, cleanup, sequence operation, or privilege change was performed.

## Remaining Stage 2 blockers and next step

Stage 2 remains `PARTIAL`. Provider-backed persistence and transaction
atomicity, failure-injection and uncertain-commit recovery, durable audit-chain
evidence, and lifecycle API acceptance are still required for the roadmap exit.
The next smallest safe step is a source-only lifecycle transition matrix and
focused tests for every specialized service entry point, followed only after
owner/provider gates by the corresponding provider-backed persistence and
failure tests.

## Worktree preservation

The branch remained `workstream-i-lifecycle-authority`. The repository already
contained extensive modified, untracked, and ignored work; it was preserved.
No reset, clean, stash, branch switch, commit, push, environment/configuration
change, or migration change was performed.
