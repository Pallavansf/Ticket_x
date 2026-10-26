# PA_TICKETS Stage 2, Slice 9 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Audited generic entry points

| Method | Target status | Ordering before this slice |
|---|---|---|
| `RouteAsync` | `ROUTED` | Assignment authorization runs first; work-item ID is allocated; generic mutation then applies domain mutation/event before configured policy. Remaining candidate. |
| `ExecuteAsync` | `IN_PROGRESS` | Assignment authorization runs first; generic mutation applies assignment/start/event before configured policy. Remaining candidate. |
| `WaitAsync` | `WAITING` | Waiting-reason validation runs first; generic mutation applies work-item/ticket mutation/event before configured policy. Remaining candidate. |
| `ResumeAsync` | `IN_PROGRESS` | Generic mutation applies resume/event before configured policy. Remaining candidate. |
| `ResolveAsync` | `RESOLVED` | Domain invariants run during mutation; status/event can precede configured policy. Remaining candidate. |
| `ReopenAsync` | `REOPENED` | Domain reason/state checks run during mutation; status/event can precede configured policy. Remaining candidate. |
| `CloseAsync` | `CLOSED` | Authorization, configured-policy preflight, domain close invariant, mutation/event, save. Corrected in this slice. |

The prior bounded fixes remain covered for classification, approval request,
approval completion, and cancellation. Specialized Incident/Problem preflight
and status checks remain covered by the existing transition matrix.

## Correction

`CloseAsync` now passes `TicketStatus.Closed` to the existing pre-mutation
policy branch. The regression starts from a valid resolved ticket, denies the
configured `RESOLVED -> CLOSED` transition, and verifies status, close fields,
domain events, and repository save count are unchanged.

## Integrity follow-up

The next highest-value Stage 2 work remains provider validation of the shared
IncidentService audit boundary. The source-level boundary and ownership tests
are complete, but PostgreSQL rollback, commit ambiguity, connection loss,
disposal, isolation, and audit-chain concurrency remain unverified. Slice 8
already established that provider execution is blocked pending attributable
TGT/OP/BASE approvals and an approved baseline/hash pair.

## Files changed

- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs` — add the
  known close target to the pre-mutation policy check.
- `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
  — add denied close preservation coverage.
- `Docs/PA_TICKETS_STAGE_2_SLICE_9_RESULT.md` — this report.

## Validation

- Focused lifecycle/transaction regression selection:
  `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **47 passed, 0 failed, 0 skipped**.
- API build:
  `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for dirty files.

No database/provider connection, SQL, migration, DDL/DML, schema/RBAC change,
privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed. All existing worktree changes were
preserved; no reset, clean, stash, branch switch, commit, or push occurred.
