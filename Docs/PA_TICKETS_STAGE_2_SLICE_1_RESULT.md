# PA_TICKETS Stage 2, Slice 1 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Selected criterion and root cause

This slice addresses the Stage 2 exit criterion requiring lifecycle mutation
guards to reject invalid or disallowed transitions before persistence and to
preserve the aggregate's in-memory state when a transition is denied.

`TicketLifecycleService.Mutate` historically applied the domain action before
consulting the configured status-transition policy. `ClassifyAsync` therefore
changed a `NEW` ticket to `CLASSIFIED`, populated classification fields, and
recorded a domain event before a denied `NEW -> TRIAGE` policy result was
observed. The repository was not called, but a tracked aggregate could remain
partially mutated and a later operation could observe those uncommitted changes.

## Correction

`ClassifyAsync` now supplies its known target status (`CLASSIFIED`) to the
existing `Mutate` preflight path. The policy is evaluated after authorization
but before `Ticket.Classify` runs. The existing specialized-transition
preflight and transaction-boundary behavior is unchanged.

The focused regression now verifies that a denied classification preserves the
status, classification fields, domain-event collection, and zero persistence
calls. This is a source-level correction only; it does not change configured
transition rules or authorization semantics.

## Files changed for this slice

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — pass the
  known classification target into the existing pre-mutation policy check.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — assert state, fields, events, and persistence remain unchanged after a
  denied classification.
- `Docs/PA_TICKETS_STAGE_2_SLICE_1_RESULT.md` — this evidence report.

These files already contained unrelated pre-existing work; that work was
preserved. The listed code/test changes are the incremental edits for this
slice.

## Validation

- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -v minimal`
  — **23 passed, 0 failed, 0 skipped**.
- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests" -v minimal`
  — **37 passed, 0 failed, 0 skipped**.
- `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; Git reported only existing LF/CRLF conversion
  notices for dirty files.

No PostgreSQL or Oracle connection was opened. No SQL, migration, DDL/DML,
schema, RBAC catalogue/grant, privilege, sequence, provisioning, seed,
cleanup, or provider-backed test operation was performed. The approved
PostgreSQL target was therefore not contacted in this slice.

## Remaining evidence and blockers

Stage 2 remains `PARTIAL`. Provider-backed persistence, rollback and commit
outcome behavior, audit-chain durability, concurrent transition behavior, and
API lifecycle acceptance remain unverified. The known PostgreSQL authorization
metadata failure (`S_MODULE_CODE` absent from the approved catalog) remains a
separate fail-closed RBAC/schema contract blocker; no workaround was applied.
The pending owner/provider approvals for the runtime transaction seam and RBAC
catalogue remain unchanged.

## Next smallest Stage 2 slice

The next safe increment is another narrowly scoped lifecycle entry-point
preflight regression using existing abstractions, selected only after source
inspection confirms a concrete gap. Provider validation remains separately
blocked until its target, schema, RBAC, and transaction evidence gates are
satisfied.

All pre-existing tracked, untracked, ignored, and local worktree changes were
preserved. No reset, clean, stash, branch switch, commit, or push occurred.
