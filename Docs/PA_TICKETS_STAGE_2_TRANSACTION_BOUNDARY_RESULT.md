# PA_TICKETS Stage 2 Transaction-Boundary Regression Result

**Branch:** `workstream-i-lifecycle-authority`  
**Stage 2 status:** `PARTIAL`

## Defect established

`EfTicketRepository.AddAsync` begins the transaction that owns ticket,
event, and audit persistence, then calls the shared `Commit` method. The
shared method previously called `t.ClearEvents()` before `AddAsync` executed
its owning `tx.CommitAsync`. A commit failure could therefore leave the
aggregate without its uncommitted events even though persistence was not
confirmed. That weakened retry/reconciliation evidence and contradicted the
transaction-outcome contract.

`SaveAsync` has a different ownership path: `Commit` begins and commits its
own transaction when no outer transaction exists. When an outer transaction
already exists, `Commit` does not commit or clear events; the outer owner
remains responsible for the final commit. `AddRelationshipAsync` owns its
transaction directly and has no aggregate event collection to clear.

## Correction

Added the internal `TicketEventCommitBoundary` helper and moved event
clearing to post-commit points:

- `AddAsync` clears events only after its owning `tx.CommitAsync` succeeds.
- A locally owned `SaveAsync` transaction clears events only after its commit
  succeeds.
- An outer-transaction `SaveAsync` path leaves events pending for the caller
  that owns the final commit.
- Any failure before the post-commit boundary leaves domain events available
  for diagnostics/reconciliation.

No transaction executor was introduced, `TransactionOutcome` was not wired
into runtime behavior, and audit-chain locking/hashing and authorization
ordering were unchanged.

## Regression tests

`TicketEventCommitBoundaryTests` covers the source-level invariant that a
failed or caller-owned commit path does not clear events and that clearing is
performed only at the successful-commit boundary. The test is intentionally
provider-independent; it does not prove that a PostgreSQL/Npgsql commit,
rollback, disposal, or connection-loss operation succeeds or fails in a
particular way.

## Validation

- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleConfiguredTransitionTests"`
  — **31 passed, 0 failed, 0 skipped**.
- `dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; only existing LF/CRLF conversion notices were
  reported for pre-existing modified files.

No database/provider connection, SQL, migration, schema/RBAC operation,
provisioning, seeding, cleanup, privilege change, commit, or push occurred.

## Remaining limits and next task

The repository still does not expose provider-independent injection for
`SaveChangesAsync`, commit, abort, disposal, or connection loss. Tracked EF
entities after non-concurrency failures and provider rollback semantics
therefore require provider-backed evidence and the separately pending
transaction-executor approval. Stage 2 remains partial.

The next unblocked source task is continued lifecycle/workflow regression
coverage using existing abstractions while owner/provider approval for the
runtime transaction seam remains outstanding.

## Preservation

All pre-existing modified, untracked, ignored, and local files were preserved.
Only the event-boundary implementation, its focused tests, and this result
report were added or changed for this increment.
