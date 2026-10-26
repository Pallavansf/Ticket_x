# PA_TICKETS Stage 2 Slice 37 Result

**Branch:** `workstream-i-lifecycle-authority`
**Stage 2:** `PARTIAL`
**Provider execution:** `NO-GO`

## Finding

The Incident creation atomicity risk is confirmed and remains safely deferred. `IncidentService.CreateAsync` calls `ITicketLifecycleService.CreateAsync` before it opens `creationTx` (`src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs:31-37`). `TicketLifecycleService.CreateAsync` calls `ITicketRepository.AddAsync` (`src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs:14-60`), while `EfTicketRepository.AddAsync` unconditionally begins, saves, commits, and clears events in its own transaction (`src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs:9`). Only after that commit does IncidentService add `IncidentRow`, the incident event, and the incident audit and save them (`IncidentService.cs:39-73`). A later failure can therefore leave a committed base ticket without its Incident row.

## Why no source change was made

The current abstractions do not expose a safe outer transaction contract. `ITicketLifecycleService.CreateAsync` and `ITicketRepository.AddAsync` have no transaction/ownership parameter (`src/PA.Tickets.Application/Tickets/TicketCommands.cs:10-11`). `AuditTransaction.BeginIfNeededAsync` only returns null when a DbContext transaction already exists, and `CommitIfOwnedAsync` cannot transfer ownership (`src/PA.Tickets.Infrastructure/Persistence/AuditTransaction.cs:5-14`). The lifecycle service is also responsible for idempotency and workflow follow-up after repository creation. Adding an outer transaction would therefore require coordinated interface and implementation changes, plus explicit event-clearing and failure-outcome semantics. A local change to IncidentService alone would not prevent the repository's independent commit.

Implementing only part of that change could create nested transactions, prematurely commit a caller-owned transaction, or clear events before a confirmed outer commit. No provider-independent fake can prove the required PostgreSQL atomicity or uncertain-commit behavior. The smallest safe design change is an infrastructure-owned unit-of-work/transaction scope passed through the lifecycle creation contract, with repository reuse of an existing transaction and event clearing performed only by the final owner after confirmed commit. That design remains pending separate approval and provider validation.

## Source execution sequence

1. Validate commander, authorization, and idempotency in IncidentService.
2. Create and persist the base ticket, work item(s), lifecycle event, audit, and idempotency result through TicketLifecycleService/EfTicketRepository's repository-owned transaction.
3. Begin IncidentService's transaction after the base commit.
4. Stage and save the Incident row, incident event, and incident audit.
5. Commit the Incident-specific transaction and reload the view.

The failure window is between steps 2 and 4. Existing `AuditTransaction` ownership tests cover local versus caller-owned commit decisions, but they cannot make the two currently separate transactions atomic.

## Validation and preservation

Inspected `IncidentService`, `TicketLifecycleService`, `ITicketLifecycleService`, `ITicketRepository`, `EfTicketRepository`, `AuditTransaction`, event clearing, Incident creation tests, and Slice 26/28 evidence. No production or test code was changed and no new test was added because the confirmed gap is a cross-service transaction contract gap rather than a safe isolated defect.

No database/provider connection, SQL, migration, DDL/DML, seed, cleanup, grant, privilege, sequence, or migration-026 operation occurred. Existing tracked, untracked, ignored, and local worktree changes were preserved. D01-D11 and TGT/OP/BASE/PRE/OP-TEST statuses remain unchanged.

`git diff --check` was run; it reports the pre-existing decision-packet whitespace findings and LF/CRLF notices. No new intentional whitespace issue was introduced by this report.

## Next action

Obtain the required attributable provider gates, then approve and implement the smallest infrastructure-owned transaction scope that lets Incident creation reuse one transaction across base ticket, Incident row, events, audits, idempotency, and workflow follow-up. Validate success, failure, caller-owned transaction, commit uncertainty, and event clearing against the authorized provider target. Stage 2 remains `PARTIAL` and provider execution remains `NO-GO`.
