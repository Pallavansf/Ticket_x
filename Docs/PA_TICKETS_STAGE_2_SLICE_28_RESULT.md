# PA_TICKETS Stage 2 Slice 28 — Owner Evidence Intake and Transaction-Fix Readiness

Branch: workstream-i-lifecycle-authority  
Stage 2 status: PARTIAL  
Provider execution: NO-GO

## Outcome

Created one consolidated owner evidence checklist and a source-grounded
implementation/test plan for the deferred IncidentService.CreateAsync
transaction boundary. No gate was resolved and no production code, migration,
configuration, or test behavior was changed.

## Files inspected and created

Inspected:

- Docs/PA_TICKETS_MASTER_ROADMAP.md
- Docs/PA_TICKETS_STAGE_2_SLICE_25_RESULT.md
- Docs/PA_TICKETS_STAGE_2_SLICE_26_RESULT.md
- Docs/PA_TICKETS_STAGE_2_SLICE_27_RESULT.md
- Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md
- Docs/reconciliation/PA_TICKETS_OWNER_APPROVAL_REQUEST.md
- tests/PA.Tickets.Domain.Tests/PostgreSqlProviderPreflight.cs
- tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironment.cs
- tests/PA.Tickets.Domain.Tests/PostgreSqlTestEnvironmentTests.cs
- src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs
- src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs
- src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs
- src/PA.Tickets.Infrastructure/Persistence/AuditTransaction.cs
- existing lifecycle, ownership, event-boundary, outcome, incident, and
  repository tests

Created:

- Docs/PA_TICKETS_STAGE_2_OWNER_EVIDENCE_INTAKE.md
- Docs/PA_TICKETS_STAGE_2_SLICE_28_RESULT.md

## Evidence intake coverage

The intake artifact defines objective acceptance criteria and attribution
fields for TGT, OP, BASE, PRE, and OP-TEST. It references the existing D01-D10
decision packet without duplicating it, preserves D11/P26 as a separate chain,
and explicitly states that the read-only preflight operation does not authorize
the write test.

Current evidence still leaves TGT, OP, BASE, and PRE blocked. OP-TEST has only
conversation-level scope input and remains partial; it cannot override the
independent gates. D01-D10 and D11 remain formally PENDING. No owner identity,
signature, date, expiry, or external submission was invented.

## Incident creation transaction plan

Current sequence:

1. IncidentService.CreateAsync validates commander and idempotency inputs.
2. It calls ITicketLifecycleService.CreateAsync.
3. EfTicketRepository.AddAsync unconditionally begins its own transaction,
   persists the base ticket/work items/events/audit, commits, and clears
   domain events.
4. Only after that return does IncidentService begin creationTx.
5. It stages the IncidentRow, incident event, and audit, saves, and commits.

This leaves a source-confirmed failure window: an incident-specific failure
after step 3 can leave a committed base ticket without an incident row.

Smallest coordinated implementation plan, deferred until approval and
provider validation:

1. Begin the outer transaction in IncidentService before lifecycle creation.
2. Change EfTicketRepository.AddAsync to call BeginIfNeeded semantics: reuse an
   existing transaction without committing or disposing it; own and commit a
   transaction only when none exists.
3. Keep lifecycle event/audit staging and incident-row/event/audit staging in
   the same DbContext transaction, with one SaveChanges boundary before the
   outer owner commits.
4. Clear aggregate domain events only after the transaction owner confirms a
   successful commit. Caller-owned paths must leave commit, rollback, disposal,
   and event clearing to the owner.
5. Preserve primary failures when rollback or disposal also fails; an ambiguous
   commit must remain unknown and must not be retried automatically.
6. Ensure cancellation before commit aborts the owned transaction and
   cancellation/connection loss during commit is recorded as outcome unknown.
7. Review other AddAsync callers for compatibility before implementation.

The plan does not claim that current disposal proves rollback or that fake
transactions prove PostgreSQL atomicity.

## Regression and provider matrix

| Scenario | Test class | Expected outcome | Evidence required |
|---|---|---|---|
| Successful incident creation | Provider-independent orchestration test, then PostgreSQL test | Ticket, incident row, lifecycle event, incident event, and audit persist together | Fresh-context reads and audit/history rows |
| Failure after base-ticket staging before incident row | Provider test with injected failure at the boundary | Confirmed abort; no durable base-only ticket | Rollback evidence and cataloged test-owned cleanup |
| Audit/event persistence failure | Provider failure injection | Confirmed abort when rollback is established; otherwise unknown | Original and cleanup exceptions plus transaction outcome |
| Caller-owned transaction | Provider test using an outer transaction | Service neither commits nor disposes caller transaction | Commit counts, outer commit result, fresh-context verification |
| Repository-owned transaction | Provider test with no outer transaction | Repository commits once and clears events only after success | Commit and event-clear evidence |
| Rollback/disposal failure | Provider failure injection | Preserve primary and cleanup failures; never claim rollback if unproven | Provider exception details retained without secrets |
| Commit failure/connection loss | Provider test | Outcome unknown; no automatic retry | Independent reconciliation and no rerun until resolved |
| Fresh-context verification | PostgreSQL test | Ticket, incident, both event classes, and audit chain agree | Readback from a new context |
| Cleanup | PostgreSQL test finally block | Delete only generated ticket/related rows and approved audit function scope | Affected-row and post-cleanup evidence |

Provider scenarios remain unauthorized and unrun. Provider-independent tests can
cover ownership decisions and pure outcome classification, but cannot establish
durable atomicity.

## Validation and publication state

Validated all referenced paths and the current DDL provenance records. Ran
git diff --check with exit code 0; existing LF/CRLF conversion notices were
reported and no whitespace errors were found. No unrelated test suite was
rerun because no code or test behavior changed.

The new documents are local-only and untracked. The branch remains
workstream-i-lifecycle-authority at the prior local/remote HEAD; this slice has
not been committed or pushed. All existing modified, untracked, ignored, and
local files were preserved.

No PostgreSQL or Oracle connection, provider test, SQL, migration, DDL/DML,
provisioning, seeding, cleanup, privilege, sequence, or migration-026
operation occurred.

## Next action

Obtain attributable TGT, OP, BASE, PRE, and OP-TEST evidence using the new
intake checklist. Only after those gates are consistent and unexpired should
the transaction-boundary change be implemented and validated against the
authorized PostgreSQL target. Stage 2 remains PARTIAL.
