# PA_TICKETS Stage 2 Lifecycle Transition Coverage Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Scope

This increment reviewed the specialized status entry points after the
`TicketLifecycleService` pre-mutation policy fix. The review remained
provider-independent; no database, SQL, migration, or provider-backed test was
run.

## Entry-point inventory

| Entry point | Status path | Validation and mutation order | Evidence |
|---|---|---|---|
| `TicketLifecycleService.TransitionSpecializedStatusAsync` | Resolved, Closed, Reopened | Authorization, domain validation, configured transition check for the intended target, aggregate mutation, then `SaveAsync` | `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs`; `src/PA.Tickets.Domain/Tickets/Ticket.cs` |
| `IncidentService.ResolveAsync` | Resolved | Incident row is changed before delegation to `TransitionSpecializedStatusAsync`; transaction is opened locally | `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs` |
| `IncidentService.ReopenAsync` | Reopened | Incident row is changed before delegation; local transaction commits after event/audit writes | `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs` |
| `IncidentService.CloseAsync` | Closed | Incident row is changed before delegation; local transaction commits after event/audit writes | `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs` |
| `ProblemService.ResolveAsync` | Resolved | `MutateAsync` changes `ProblemRow` first, then delegates to the lifecycle service | `src/PA.Tickets.Infrastructure/Tickets/ProblemService.cs` |
| `ProblemService.CloseAsync` | Closed | `MutateAsync` changes `ProblemRow` first, then delegates to the lifecycle service | `src/PA.Tickets.Infrastructure/Tickets/ProblemService.cs` |
| `ProblemService.ReopenAsync` | Reopened | `MutateAsync` changes `ProblemRow` first, then delegates to the lifecycle service | `src/PA.Tickets.Infrastructure/Tickets/ProblemService.cs` |
| `ChangeService` workflow methods | Change-specific `ChangeStatus` values (Draft through Reopened) | These are subtype workflow transitions and do not call `TransitionSpecializedStatusAsync` or change the parent `TicketStatus` | `src/PA.Tickets.Infrastructure/Tickets/ChangeService.cs` |

The shared lifecycle service now checks the configured policy against the
requested target before changing the aggregate. The specialized infrastructure
services still mutate their provider-tracked subtype row before that check is
reached. Their local transaction is a provider behavior boundary; this source
review does not prove rollback or tracked-state restoration after a denied
transition. Change workflow status is a separate contract and is not evidence
that the parent ticket lifecycle policy was applied.

## Regression coverage added

`Denied_specialized_status_matrix_preserves_state_and_events` extends the
provider-independent matrix for denied `Resolved`, `Closed`, and `Reopened`
targets. For each target it verifies:

- the ticket status remains unchanged;
- the uncommitted domain-event sequence is unchanged; and
- `SaveAsync` is never called.

The existing single-target denial, authorization-ordering, configured-policy,
and persistence-failure tests remain in
`tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`.

## Owner-selected preflight seam

The database owner selected the conservative preflight contract as the intended
V1 policy in the current working instructions. The implementation now adds
`EnsureSpecializedStatusTransitionAllowedAsync` to `ITicketLifecycleService`.
It authorizes, validates domain invariants, and checks the configured target
transition without mutating, saving, writing an audit row, or consuming events.
Incident and Problem call it before changing their subtype rows; the existing
`TransitionSpecializedStatusAsync` call still repeats the check inside the
transaction immediately before parent mutation.

This implements the selected policy but is not a signed repository approval.
Provider rollback, concurrent policy changes, commit ambiguity, and audit
durability remain unverified.

## Validation

- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests"` — **21 passed, 0 failed, 0 skipped**.
- `dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo` — **0 warnings, 0 errors**.
- `git diff --check` — passed; only normal existing LF/CRLF conversion notices were emitted.

Additional preflight-seam validation:

- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~WorkstreamHRegressionTests"` — **28 passed, 0 failed, 0 skipped**.
- `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo` — **0 warnings, 0 errors**.

No database/provider connection, SQL, migration, schema/RBAC operation,
provisioning, seeding, cleanup, privilege change, commit, or push occurred.

## Remaining blockers and next task

Stage 2 remains `PARTIAL`. Provider-backed persistence, transaction rollback and
commit-outcome evidence, audit-chain durability, concurrency/TOCTOU behavior,
and lifecycle API acceptance remain unverified. The next concrete step is
formal attribution of the selected preflight policy followed by provider-backed
transaction and concurrency validation.

All pre-existing tracked, untracked, ignored, and local files were preserved.
