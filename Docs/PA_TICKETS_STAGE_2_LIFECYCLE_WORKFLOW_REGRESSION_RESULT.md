# PA_TICKETS Stage 2 Lifecycle/Workflow Regression Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Defect established

`TicketLifecycleService.Mutate` previously invoked the aggregate action before
checking the configured status-transition policy. The specialized status
entry point therefore changed a shared/managed aggregate and appended a domain
event before a denied policy result was raised. `SaveAsync` was not called,
but the in-memory aggregate was already changed. Existing tests checked the
save count but did not check status/event preservation for this path.

## Correction

`Ticket.ValidateSpecializedStatusTransition` now performs the domain invariant
checks without mutation. The specialized lifecycle path uses a pre-mutation
validation hook and checks the configured policy against the intended target
status before invoking `TransitionSpecializedStatus`. Existing generic
lifecycle paths retain their prior ordering and behavior.

This preserves authorization before mutation, keeps policy failures out of
persistence, and avoids changing transaction ownership, audit-chain behavior,
RBAC semantics, or public API contracts.

## Regression coverage

`Denied_specialized_transition_does_not_mutate_before_policy_check` verifies
that a denied `NEW -> RESOLVED` specialized transition leaves the status and
uncommitted events unchanged and does not call `SaveAsync`.

## Validation

- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests"`
  — **32 passed, 0 failed, 0 skipped**.
- `dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for pre-existing modified files.

No database/provider connection, SQL, migration, schema/RBAC operation,
provisioning, seeding, cleanup, privilege change, commit, or push occurred.

## Remaining evidence gaps and next task

The regression is source-level only. Provider-backed persistence, transaction
atomicity, uncertain commit recovery, audit-chain durability, concurrency,
and lifecycle API acceptance remain unverified. Stage 2 remains partial.

The next unblocked task is to extend the source-level lifecycle transition
matrix to the remaining specialized service entry points using existing fakes;
provider-backed validation remains gated by the approved schema, RBAC, and
transaction/recovery decisions.

## Preservation

All pre-existing modified, untracked, ignored, and local files were preserved.
Only the lifecycle/domain correction, focused regression test, and this result
report were added or changed for this increment.
