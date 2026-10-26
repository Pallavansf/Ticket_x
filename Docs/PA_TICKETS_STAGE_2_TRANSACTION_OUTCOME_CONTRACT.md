# PA_TICKETS Stage 2 Transaction Outcome Contract

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** design and provider-independent test contract only  
**Status:** proposed; not implemented or approved

## Purpose and boundary

The existing infrastructure keeps ticket, event, and audit-chain writes in one
EF transaction. This note defines the outcome contract a future,
infrastructure-owned execution seam must preserve. It does not move transaction
ownership into the application layer, change `ITicketRepository`, add a test
hook, or claim PostgreSQL/Npgsql behavior has been verified.

The application may return a successful lifecycle result only after the
transaction boundary it owns has committed. An outer-transaction caller may
return only an explicitly non-committed/prepared result until its owner commits.
No uncertain outcome may be retried automatically.

## Outcome model

Every execution has one terminal classification:

- **`CONFIRMED_COMMITTED`** — the provider returned successful commit completion
  for the transaction owned by this execution.
- **`CONFIRMED_ABORTED`** — the provider explicitly confirmed rollback/abort
  completion, or the work failed before a transaction began and no transaction
  existed. An exception alone is insufficient evidence.
- **`OUTCOME_UNKNOWN`** — commit, disposal, connection loss, or cleanup left it
  unprovable whether the transaction committed. This state is non-retryable
  until independent reconciliation establishes the result.
- **`OUTER_COMMIT_PENDING`** — work completed inside a caller-owned transaction;
  this is not committed evidence and cannot be reported as success to an
  execution boundary that requires commit.

The result must preserve the primary exception, any cleanup exception, and
whether the transaction was owned locally or externally. If cleanup also fails,
the primary failure remains the primary diagnostic and the cleanup failure is
retained as additional diagnostic context. The result must never claim rollback
success when abort or disposal failed.

## Required execution invariants

1. Begin, ticket state writes, event writes, audit-chain lock acquisition,
   predecessor lookup, audit-row writes, and `SaveChangesAsync` remain inside
   one transaction when the infrastructure owns the transaction.
2. Audit rows are not committed independently of ticket/event changes.
3. A known persistence failure is surfaced without a successful lifecycle
   result. Automatic retry is allowed only after `CONFIRMED_ABORTED`, never
   after `OUTCOME_UNKNOWN`.
4. Authorization remains before aggregate mutation and persistence.
5. An outer transaction is never committed or aborted by the nested executor;
   its owner receives `OUTER_COMMIT_PENDING` after successful work and controls
   the final outcome.
6. Domain events are cleared only after the required local commit succeeds.

## Failure mapping

| Failure point | Required classification | Required behavior |
|---|---|---|
| Begin fails before a transaction exists | `CONFIRMED_ABORTED` only when provider confirms no transaction; otherwise `OUTCOME_UNKNOWN` | Return failure; no mutation success; retain diagnostics |
| Ticket/event/audit tracking or `SaveChangesAsync` fails before commit | `CONFIRMED_ABORTED` only after successful abort confirmation; otherwise `OUTCOME_UNKNOWN` | Preserve primary error; retain cleanup error; no retry unless aborted is proven |
| Commit returns success | `CONFIRMED_COMMITTED` | Clear events and return success only after this boundary |
| Commit throws, connection drops, or provider cannot report completion | `OUTCOME_UNKNOWN` unless provider independently confirms abort | Do not retry or claim rollback; require reconciliation |
| Abort/rollback fails after an earlier failure | `OUTCOME_UNKNOWN` | Preserve both exceptions; no success or retry |
| Disposal fails before outcome is known | `OUTCOME_UNKNOWN` unless commit/abort was independently confirmed | Preserve disposal failure as cleanup diagnostic |
| Outer transaction work succeeds | `OUTER_COMMIT_PENDING` | Do not commit; outer owner must complete and classify the transaction |
| Outer transaction owner commit fails or becomes ambiguous | Owner classifies `CONFIRMED_ABORTED` or `OUTCOME_UNKNOWN` using provider evidence | Nested operation must not claim success or retry |

## Outer-transaction behavior

The current `AuditTransaction.BeginIfNeededAsync` returns no owned transaction
when `DbContext.Database.CurrentTransaction` is already set. The future seam
must retain that behavior. In the outer case it must not call `CommitAsync`,
`RollbackAsync`, or dispose the caller's transaction. It may report that all
work and `SaveChangesAsync` completed, but the terminal execution state remains
`OUTER_COMMIT_PENDING` until the owner reports the final commit outcome.

## Provider-independent contract tests required before implementation

These tests should target a pure fake executor contract, without opening a
database or changing DI:

1. Begin success, successful work, successful commit → `CONFIRMED_COMMITTED`.
2. Work failure with successful abort → `CONFIRMED_ABORTED`; primary exception
   preserved.
3. Work failure with abort failure → `OUTCOME_UNKNOWN`; both exceptions
   preserved and no retry signal.
4. Commit failure with explicit provider-confirmed abort →
   `CONFIRMED_ABORTED`.
5. Commit failure without outcome evidence → `OUTCOME_UNKNOWN`; no success and
   no automatic retry.
6. Connection loss during commit → `OUTCOME_UNKNOWN`.
7. Disposal failure before outcome evidence → `OUTCOME_UNKNOWN`.
8. Outer transaction → no begin/commit/abort calls by the nested executor and
   `OUTER_COMMIT_PENDING` result.
9. Audit work is invoked inside the same execution callback; an audit failure
   follows the same abort/unknown mapping as any other persistence failure.
10. Authorization denial occurs before the transaction callback is invoked.

These are contract tests, not evidence of PostgreSQL rollback, Npgsql commit
semantics, advisory-lock behavior, or connection-loss recovery. Provider-backed
tests must later verify each provider-specific assumption separately.

## Minimal implementation plan

1. Obtain owner/provider approval for the outcome vocabulary, reconciliation
   record, and diagnostic-retention requirements.
2. Add one infrastructure-owned executor used by `EfTicketRepository`; keep
   `ITicketRepository` and application transaction ownership unchanged.
3. Represent primary and cleanup exceptions without exposing credentials or
   connection strings.
4. Keep audit-chain locking and hash construction inside the executor callback.
5. Add the contract tests above against a fake executor, then run provider
   failure-injection tests for PostgreSQL/Npgsql before relying on the result
   states.
6. Add recovery/reconciliation handling for `OUTCOME_UNKNOWN`; never convert
   it into a retryable failure by default.

## Approval questions and acceptance criteria

Owner/provider approval is required for: the exact provider evidence that can
prove abort after commit failure; retention and custody of unknown-outcome
records; whether disposal failure is always unknown; outer-transaction result
handling; and the reconciliation procedure before a subsequent attempt.

Implementation acceptance requires that all contract tests pass, authorization
ordering is unchanged, ticket/event/audit writes remain co-transactional, no
uncertain result is retried, primary and cleanup failures are retained, and
provider-backed tests demonstrate the claimed classifications. Stage 2 must
remain `PARTIAL` until those runtime and lifecycle API criteria are evidenced.

## Next unblocked roadmap task

The next safe task is implementation of the approved pure outcome model and
contract tests around an infrastructure-owned executor, without provider access.
If owners do not approve that seam, continue source-level lifecycle/workflow
coverage using existing abstractions; do not alter the transaction model to
manufacture green tests.

No database/provider operations, SQL, migrations, schema/RBAC changes, commits,
or pushes were performed for this design note.
