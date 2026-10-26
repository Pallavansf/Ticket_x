# PA_TICKETS Stage 2 Failure-Injection Test Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** database-independent lifecycle failure tests  
**Status:** `PARTIAL` — failure propagation and authorization ordering are
covered; provider transaction/audit atomicity remains unverified.

## Changes

Only `tests/PA.Tickets.Domain.Tests/TicketLifecycleConfiguredTransitionTests.cs`
was changed as application/test source in this sprint. The result report was
also created. The existing test doubles now support:

- Injecting a persistence exception from `ITicketRepository.SaveAsync`.
- Counting mutation-authorization checks and denying them before mutation.
- Observing whether the fake repository reports a successful save. This is not
  an audit-write observation.

Added tests:

- `Persistence_failure_does_not_return_a_successful_lifecycle_result`
- `Authorization_denial_prevents_mutation_persistence_and_audit_attempts`

No production defect was fixed. The tests use the existing
`TicketLifecycleService` and `ITicketRepository` boundary without introducing
another transaction or audit design.

## Evidence and limits

The persistence-failure test proves that the thrown repository exception is
propagated, no `TicketView` success result is returned, authorization ran first,
and the fake repository did not report a successful save. The test
also makes the current architectural limitation visible: the service mutates
the in-memory aggregate before calling `SaveAsync`, so the aggregate remains
`Classified` after the injected failure. This is not evidence of partial
database persistence; it is an application-level rollback gap that requires a
separate design decision or provider-backed transaction evidence.

The authorization-ordering test proves that a denied mutation leaves the ticket
status and pending events unchanged and never calls persistence. It does not
prove provider authorization behavior.

Audit-write failure and transaction commit/abort failure could not be injected
through the current application abstraction. `ITicketRepository` exposes only
`SaveAsync`; audit writing and transaction begin/commit/rollback are internal to
infrastructure repositories such as `EfTicketRepository` and
`AuditChainCoordinator`. No new abstraction was added in this sprint. Those
paths therefore remain provider/infrastructure validation work.

## Validation

Build:

```text
dotnet build tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\stage2-failure-injection\bin\ -v minimal
```

Result: succeeded, 0 warnings, 0 errors.

Focused tests:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~TicketLifecycleConfiguredTransitionTests" -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\stage2-failure-injection\bin\ -v minimal
```

Result: 17 passed, 0 failed, 0 skipped. No provider reachability tests were
selected.

## Remaining Stage 2 gaps and next step

Stage 2 remains `PARTIAL`. Provider-backed persistence atomicity, real audit
write failure, commit/rollback failure, uncertain-commit recovery, durable
audit-chain evidence, and lifecycle API acceptance remain outstanding. RBAC
decisions, permission metadata, migrations, and schema state were untouched.

The next safe roadmap task is to review the existing infrastructure transaction
seams and design a narrowly scoped failure-injection seam, if owner/provider
constraints permit it; otherwise proceed with explicitly authorized provider
failure tests. Neither path is authorized by this sprint.

## Preservation and safety

No PostgreSQL or Oracle connection, SQL, migration, DDL, DML, provisioning,
seeding, cleanup, sequence operation, privilege change, or provider-backed test
was performed. Migrations 026 and 030–038, RBAC data, and roadmap status were
not changed. The branch and all pre-existing modified, untracked, ignored, and
local configuration files were preserved. No reset, clean, stash, branch switch,
commit, or push occurred.
