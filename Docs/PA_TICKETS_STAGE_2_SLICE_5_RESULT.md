# PA_TICKETS Stage 2, Slice 5 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Defect and transaction boundary

Before this slice, `IncidentService` called `AddAuditAsync` from incident
creation and six mutation methods without an outer transaction. Because
`AuditTransaction.BeginIfNeededAsync` starts and returns an owned transaction
when `CurrentTransaction` is null, the audit write could commit before the
following incident/event `SaveChangesAsync`. A later persistence failure could
therefore leave an audit-only success.

The service now uses one shared `ExecuteMutationAsync` boundary for
acknowledgement, commander assignment, major declaration, restore, impact, and
urgency updates. It begins an audit transaction only when the DbContext has no
current transaction, stages the mutation/event and audit together, saves once,
and commits only if it owns the transaction. Incident creation begins the same
boundary after the lifecycle service has created the ticket, then commits after
the incident row, event, and audit are staged. This preserves the ticket
lifecycle service's event-clearing ownership. When a caller-owned transaction exists,
`BeginIfNeededAsync` returns null and the service does not commit it.

The boundary preserves authorization, domain checks, audit ordering, event
semantics, and public API responses. Disposal remains responsible for provider
rollback on an exception; a failed commit is not classified as confirmed
rollback by this source change.

## Tests

Added `AuditTransactionOwnershipTests` with provider-independent checks that:

- a caller-owned (`null`) transaction is not committed by
  `CommitIfOwnedAsync`; and
- a locally owned transaction is committed by the helper.

These tests validate the ownership decision, not PostgreSQL durability or
rollback behavior. Existing lifecycle, event-boundary, and transaction-outcome
tests remain unchanged and passing.

## Files changed

- `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs` — shared
  transaction boundary for incident creation and non-lifecycle mutations.
- `tests/PA.Tickets.Domain.Tests/AuditTransactionOwnershipTests.cs` — focused
  ownership tests using a fake transaction only.
- `Docs/PA_TICKETS_STAGE_2_SLICE_5_RESULT.md` — this report.

Pre-existing changes in all other files were preserved.

## Validation

- `dotnet build src\\PA.Tickets.Api\\PA.Tickets.Api.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleAuthorityTests" -v minimal`
  — **46 passed, 0 failed, 0 skipped**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for dirty files.

No PostgreSQL or Oracle connection, SQL, migration, DDL/DML, schema/RBAC data
change, privilege change, sequence operation, provisioning, seed, cleanup, or
provider-backed test was performed.

## Remaining provider evidence

The source boundary now prevents intentional independent audit commits in the
reviewed IncidentService paths, but PostgreSQL/Npgsql validation is still
required for actual commit, rollback, connection-loss, disposal, isolation,
and concurrent audit-chain behavior. The existing RBAC catalogue mismatch
(`S_MODULE_CODE` absent) still blocks authorized lifecycle provider tests.
Stage 2 exit criteria for provider persistence, failure injection, audit-chain
durability, concurrency, and API acceptance remain open.

All tracked, untracked, ignored, and local worktree changes were preserved. No
reset, clean, stash, branch switch, commit, or push occurred.
