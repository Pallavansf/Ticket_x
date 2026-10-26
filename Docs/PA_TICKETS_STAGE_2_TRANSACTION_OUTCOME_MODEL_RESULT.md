# PA_TICKETS Stage 2 Transaction Outcome Model Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** provider-independent outcome model and contract tests  
**Stage 2 status:** `PARTIAL`

## Implementation

Added `TransactionOutcome` and `TransactionOutcomeMapping` under
`src/PA.Tickets.Infrastructure/Persistence/TransactionOutcome.cs`.
The model represents:

- `ConfirmedCommitted`
- `ConfirmedAborted`
- `OutcomeUnknown`
- `OuterCommitPending`

It retains transaction ownership, primary failure, and cleanup failure. Only a
confirmed abort reports `RetryAllowed`; an uncertain commit never becomes
retryable. An outer transaction is represented as caller-owned and pending.
Contradictory abort signals (a cleanup exception alongside an asserted abort)
are classified as unknown.

No runtime transaction path, dependency injection registration,
`EfTicketRepository`, `AuditTransaction`, audit-chain code, or public API was
changed. The current infrastructure seam remains pending owner/provider
approval.

## Contract tests

Added `tests/PA.Tickets.Domain.Tests/TransactionOutcomeTests.cs` with fake,
provider-independent coverage for:

1. successful commit;
2. work failure with confirmed abort;
3. work failure with abort failure and both exceptions preserved;
4. commit failure with explicit abort evidence;
5. ambiguous commit failure;
6. connection loss during commit;
7. disposal failure before outcome evidence;
8. caller-owned outer transaction;
9. audit callback failure using the same mapping; and
10. authorization denial before the callback.

These tests validate the pure contract only. They do not prove PostgreSQL or
Npgsql rollback, commit, disposal, locking, connection-loss, or recovery
behavior, and they do not prove ticket/event/audit atomicity.

## Validation

- `dotnet test tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~TicketLifecycleConfiguredTransitionTests"`
  — **28 passed, 0 failed, 0 skipped** (11 new outcome tests and 17 existing
  lifecycle tests).
- `dotnet build tests\\PA.Tickets.Domain.Tests\\PA.Tickets.Domain.Tests.csproj --no-restore --nologo`
  — **0 warnings, 0 errors**.
- `git diff --check` — passed; Git reported only existing LF/CRLF conversion
  notices for pre-existing modified files.

No database/provider connection, SQL, migration, schema/RBAC operation,
provisioning, seeding, cleanup, privilege change, commit, or push occurred.

## Approval and remaining gaps

No repository evidence records owner/provider approval for a runtime
transaction executor, outcome reconciliation, or diagnostic-retention policy.
`EfTicketRepository` still owns its current local transaction behavior and
outer transactions remain governed by existing infrastructure. Provider-backed
failure injection is still required before any outcome classification can be
claimed as runtime evidence.

The next unblocked roadmap task is to obtain approval for the infrastructure
executor seam and then implement it with provider-independent adapter tests.
Until that approval exists, source-level lifecycle/workflow regression coverage
can continue without changing transaction ownership. Stage 2 remains partial.

## Worktree preservation

All pre-existing modified, untracked, ignored, and local files were preserved.
The only files added by this increment are the outcome model, its tests, and
this result report.
