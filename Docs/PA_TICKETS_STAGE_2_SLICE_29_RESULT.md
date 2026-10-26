# PA_TICKETS Stage 2 Slice 29 — Transaction Failure Semantics and Regression Coverage

Branch: workstream-i-lifecycle-authority  
Stage 2 status: PARTIAL  
Provider execution: NO-GO

## Conclusion

The provider-independent transaction outcome model and ownership tests already
cover the unambiguous source contract. No new source-level defect can be
corrected safely without changing runtime transaction behavior or assuming
provider semantics. No production or test code was changed. The deferred
IncidentService.CreateAsync atomicity risk remains open.

## Failure-semantics matrix

| Case | Current implementation | Existing evidence | Remaining limitation |
|---|---|---|---|
| Before transaction creation | Validation/query failures occur before the local transaction helper; exception propagates | Lifecycle and service validation tests | Provider connection/open failure behavior is unverified |
| During staging | Entity/event/audit staging throws before the owned commit; async disposal is relied on for cleanup | TransactionOutcome mapping tests model work failure; lifecycle denial tests prove no mutation before rejection | Fakes do not prove provider rollback |
| Save failure | SaveChangesAsync propagates; owned transaction disposal is expected to abort | Outcome tests classify confirmed versus unknown abort only when explicit evidence is supplied | PostgreSQL rollback and tracked-state behavior require provider evidence |
| Commit failure | CommitAsync exception propagates; no success result is constructed afterward | TransactionOutcomeTests cover known and ambiguous commit mappings | A thrown provider commit exception does not establish rollback or commit outcome |
| Rollback failure after primary failure | Mapping returns OutcomeUnknown and preserves primary plus cleanup exception when abort is not confirmed | WorkFailed and abort-failure tests | Runtime preservation through EF disposal is not provider-tested |
| Disposal failure | Await-using disposal is relied on by services; a disposal exception may affect the surfaced exception | DisposalFailed mapping test covers classification only | Actual exception ordering and provider disposal behavior require provider injection |
| Cancellation before commit | Cancellation propagates from staging or SaveChangesAsync; owned transaction disposal is expected to abort | Existing cancellation-aware APIs and outcome vocabulary | Confirmed abort requires provider evidence |
| Connection loss/cancellation during commit | No runtime classification is emitted by current services; caller receives provider exception | ConnectionLostDuringCommit mapping test defines unknown outcome | No provider evidence or durable reconciliation path exists |
| Caller-owned transaction | AuditTransaction.BeginIfNeededAsync returns null and CommitIfOwnedAsync performs no commit | AuditTransactionOwnershipTests | Outer owner must perform final commit/rollback; runtime integration is unverified |
| Repository-owned transaction | EfTicketRepository creates and commits a local transaction; events clear only after successful owned commit | TicketEventCommitBoundaryTests and ownership tests | PostgreSQL commit and event durability remain unverified |
| Domain-event clearing | TicketEventCommitBoundary.ClearAfterSuccessfulCommit is invoked after owned AddAsync commit and after owned repository Commit; caller-owned paths retain events | TicketEventCommitBoundaryTests | Cross-service outer commit has no runtime integration proof |

## Concrete gap assessment

The source confirms that IncidentService.CreateAsync calls the lifecycle
creation service before starting its incident-specific transaction. The base
ticket can therefore commit before the IncidentRow, incident event, and
incident audit are staged. This is the same cross-service atomicity risk
recorded in Slice 26 and is intentionally excluded from this slice.

The remaining possible concerns—rollback/disposal exception ordering,
connection-loss classification, and event clearing across an outer
transaction—cannot be proven or safely corrected from the current fake
abstractions without introducing a runtime transaction executor or changing
provider semantics. The task explicitly prohibits that redesign here.

## Existing tests inspected

- tests/PA.Tickets.Domain.Tests/TransactionOutcomeTests.cs
- tests/PA.Tickets.Domain.Tests/AuditTransactionOwnershipTests.cs
- tests/PA.Tickets.Domain.Tests/TicketEventCommitBoundaryTests.cs
- tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs
- tests/PA.Tickets.Domain.Tests/TicketLifecycleAuthorityTests.cs
- tests/PA.Tickets.Domain.Tests/PostgreSqlAuditTransactionTests.cs
- tests/PA.Tickets.Domain.Tests/PostgreSqlTicketPersistenceTests.cs

The first three suites provide provider-independent contract evidence. The
PostgreSQL suites were inspected only and were not executed.

## Validation

No code or test behavior changed, so no unrelated test suite was rerun.
git diff --check exited 0 with existing LF/CRLF conversion notices and no
whitespace errors.

No PostgreSQL or Oracle connection, provider test, SQL, migration, DDL/DML,
provisioning, seed, cleanup, privilege, sequence, or migration-026 operation
occurred.

## Deferred Incident creation correction

The eventual implementation must begin an outer transaction before lifecycle
ticket creation, make EfTicketRepository.AddAsync reuse a caller-owned
transaction without committing or disposing it, stage all ticket/incident
events and audits before one owner commit, and clear domain events only after
confirmed commit. It must preserve primary failures when rollback or disposal
also fails and classify commit connection loss as unknown. Provider tests must
verify fresh-context ticket, incident, event, and audit records plus
test-owned cleanup.

## Publication and preservation

The new report is local-only and untracked. Existing modified, untracked, and
ignored files remain unchanged and unpublished; the branch was not switched and
no commit or push was performed. Stage 2 remains PARTIAL and provider
execution remains NO-GO pending owner evidence and provider validation.
