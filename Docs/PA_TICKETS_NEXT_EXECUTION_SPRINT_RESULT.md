# PA_TICKETS Next Execution Sprint Result

**Repository:** `G:\\WSS\\aw\\Tickets`  
**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** source-only lifecycle/workflow implementation and verification

## Selected requirement and defect

The roadmap keeps Stage 2 lifecycle/workflow work `PARTIAL` while provider,
transaction, audit, and recovery evidence remains blocked. The next safe
source increment was a workflow-step state invariant that does not depend on
RBAC data or a database.

`WorkflowStepInstance.Fail` previously rejected only completed and cancelled
steps. Consequently, `WorkflowInstance.FailStep` could fail a pending step or
overwrite an already failed step. That contradicted the neighboring lifecycle
rules: completion requires an active or waiting step, waiting requires an
active step, and terminal step outcomes must not be rewritten.

## Implementation

Changed `src/PA.Tickets.Domain/Workflow/WorkflowInstance.cs` so failure is
accepted only from `Active` or `Waiting`. Pending, completed, cancelled, and
already failed steps now fail closed with an `InvalidOperationException`.
The existing cancellation behavior and event shape were preserved.

Added `WorkflowApprovalSlaTests.Failing_workflow_step_requires_active_or_waiting_state`
in `tests/PA.Tickets.Domain.Tests/UnitTest1.cs`. The regression proves that a
pending step cannot fail and that a failed step cannot be failed again.

## Validation actually run

Build (isolated output, no restore):

```text
dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\next-execution-sprint\\bin\\ -v minimal
```

Result: **succeeded, 0 warnings, 0 errors**.

Focused lifecycle/workflow run:

```text
dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~WorkflowApprovalSlaTests|FullyQualifiedName~TicketLifecycleTests" -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\next-execution-sprint\\bin\\ -v minimal
```

Result: **12 passed, 0 failed, 0 skipped**.

Broader database-independent regression selection:

```text
dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~TicketLifecycleTests|FullyQualifiedName~WorkflowApprovalSlaTests|FullyQualifiedName~ReportExportFormatterTests|FullyQualifiedName~ApplicationEffectiveGrantAuthorizationTests" -p:BaseOutputPath=G:\\WSS\\aw\\Tickets\\temp\\next-execution-sprint\\bin\\ -v minimal
```

Result: **29 passed, 0 failed, 0 skipped**. Provider reachability and all
database-backed tests were excluded.

`git diff --check` was run; it reported only pre-existing whitespace warnings
in unrelated files and the existing provisioning script. No files were
normalized or cleaned.

## Provider and migration boundary

This sprint opened no PostgreSQL or Oracle connection and executed no SQL,
DDL, DML, migration, provisioning, seed, cleanup, sequence, privilege, or
provider-backed test operation. Migrations 030--038 and migration 026 remain
blocked by the existing RBAC catalogue, owner-approval, identity, ledger, and
audit-actor prerequisites. No permission rows, grants, or migration history
were invented.

## Preservation and next slice

The branch remained `workstream-i-lifecycle-authority`. All pre-existing
modified, staged, untracked, and ignored work was preserved; no reset, clean,
stash, branch switch, commit, or push was used. This sprint touched the
workflow source and test listed above and created this result document; other
pre-existing worktree changes were left intact.

The next executable implementation slice is another source-backed lifecycle
or workflow invariant that can be tested without persisted RBAC or provider
behavior. Stage 2 remains `PARTIAL` until provider-backed persistence,
failure-injection, audit-chain, and lifecycle API evidence exists.

**NEXT EXECUTION SPRINT COMPLETE — WORKFLOW FAILURE STATE HARDENED; PROVIDER AND MIGRATION GATES REMAIN BLOCKED**
