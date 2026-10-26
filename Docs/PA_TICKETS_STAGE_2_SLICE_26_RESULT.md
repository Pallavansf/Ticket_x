# PA_TICKETS Stage 2 Slice 26 — Incident/Problem Lifecycle Source-Level Gap Closure

Branch: workstream-i-lifecycle-authority  
Stage 2 status: PARTIAL  
Provider decision: NO-GO

## Result

The bounded source review did not establish a new defect that can be corrected
and proven safely without provider access. Existing lifecycle preflight and
transaction ownership corrections remain intact. One high-value transaction
boundary risk was confirmed but is explicitly deferred because its correction
requires coordinated provider transaction behavior and persistence evidence.

No production source or test behavior was changed in this slice. Only this
result report was added.

## Roadmap criterion reviewed

Stage 2 requires durable lifecycle transitions, transaction and audit
correctness, concurrency behavior, and provider-backed persistence evidence.
The source-level transition and ownership portions have focused regression
coverage, while provider translation, atomicity under failures, uncertain
commit/recovery, concurrent audit-chain behavior, and API acceptance remain
open. The roadmap status therefore remains PARTIAL.

## Source matrix

| Operation/path | Preflight and invariants | Transaction/audit boundary | Source-level disposition |
|---|---|---|---|
| Generic lifecycle methods in TicketLifecycleService | Mutate authorizes, runs the supplied pre-mutation validator, checks the configured target before action, then saves | Repository owns a transaction when no caller transaction exists; events clear only after an owned commit | Existing tests cover denied target preservation; no new defect established |
| Incident Acknowledge, AssignCommander, DeclareMajor, Restore, UpdateImpact, UpdateUrgency | Authorization and subtype checks precede mutation | Shared ExecuteMutationAsync stages event/audit, saves, and commits only an owned transaction | Slice 5 correction remains source-consistent; provider rollback/commit behavior unverified |
| Incident Resolve, Reopen, Close | Authorization, subtype preflight, and status transition call precede subtype mutation | Explicit transaction encloses ticket status, subtype row, event, audit, save, and commit | No additional source-only defect established |
| Problem Resolve, Close, Reopen | MutateAsync authorizes and invokes status preflight before the mutation delegate | Explicit transaction encloses subtype mutation, lifecycle transition, event, audit, save, and commit | No additional source-only defect established |
| Problem non-lifecycle mutations | Authorization and domain guards precede field mutation | Same MutateAsync transaction/audit path | No additional source-only defect established |
| Relationship and creation paths | Authorization and relationship rules are checked before persistence | Repository owns its local transaction | Covered by existing source/tests; provider behavior remains required |

## Confirmed deferred risk

IncidentService.CreateAsync calls ITicketLifecycleService.CreateAsync
before it starts creationTx. The lifecycle repository path therefore persists
the base ticket and its lifecycle event in its own transaction before the
incident row, incident event, and incident audit are staged. A later failure
while writing the incident-specific records can leave a persisted base ticket
without the corresponding incident row.

Closing this gap safely would require starting an outer transaction before the
lifecycle creation call and changing EfTicketRepository.AddAsync to reuse a
caller-owned transaction rather than unconditionally beginning one. That
cross-layer change needs provider-backed transaction verification, including
commit, rollback, disposal, and uncertain-outcome behavior. No
provider-independent test in the current architecture can prove those
semantics honestly, so no speculative correction was made.

The same review confirms that AuditTransaction.BeginIfNeededAsync returns
null for an existing transaction and CommitIfOwnedAsync does not commit a
caller-owned transaction. Those ownership rules are covered by existing fake
transaction tests. They do not prove PostgreSQL atomicity.

## Existing regression evidence

The inspected tests cover:

- configured-transition denial with status, fields, domain events, and save
  count preserved;
- lifecycle authority and specialized transition behavior;
- local versus caller-owned audit transaction commit decisions;
- event clearing after a successful repository-owned commit;
- provider-independent transaction outcome vocabulary and migration failure
  semantics.

No new test was added because the only newly confirmed gap is the
cross-service/provider transaction boundary described above.

## Validation

The affected API build passed:

    dotnet build src\PA.Tickets.Api\PA.Tickets.Api.csproj --no-restore --nologo

Result: 0 warnings, 0 errors.

The first test invocation was started concurrently with the build and hit a
temporary output-file lock. It was rerun sequentially after the build:

    dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests|FullyQualifiedName~TicketLifecycleAuthorityTests|FullyQualifiedName~AuditTransactionOwnershipTests|FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~TransactionOutcomeTests|FullyQualifiedName~WorkstreamHRegressionTests" -v minimal

Result: 58 passed, 0 failed, 0 skipped.

git diff --check exited 0. Git emitted existing LF/CRLF conversion notices;
no whitespace errors were reported.

## Remaining blockers and next step

Stage 2 remains PARTIAL. Provider-backed persistence, failure injection,
rollback and uncertain commit classification, audit-chain durability and
concurrency, and lifecycle API acceptance remain unverified. TGT, OP, BASE,
PRE, and operation-specific authorization gates remain unresolved; provider
execution is NO-GO.

The next implementation step should be selected only after the owner evidence
gates are resolved: validate the Incident/Problem creation and mutation
transaction boundary against the authorized PostgreSQL target, including
failure and recovery observations. Until then, no further repetitive
preflight or speculative transaction redesign is justified.

Local-versus-remote state was not changed by this slice. The branch remains
aligned at its prior HEAD with the remote, while the pre-existing local
modified and untracked work remains unpublished. No database/provider
operation, migration, SQL, DDL/DML, provisioning, seed, cleanup, privilege,
sequence, commit, push, reset, clean, stash, merge, rebase, or branch switch
occurred.
