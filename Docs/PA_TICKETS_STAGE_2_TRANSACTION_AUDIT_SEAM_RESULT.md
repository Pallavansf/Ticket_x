# PA_TICKETS Stage 2 Transaction/Audit Failure-Injection Seam Review

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** source-only transaction and audit seam review  
**Status:** `PARTIAL` — current atomicity path is documented; safe injectable
failure points require a later design/provider-validation step.

## Source execution sequence

For a normal lifecycle mutation, `TicketLifecycleService.Mutate` performs these
steps:

1. Loads the aggregate through `ITicketRepository.GetAsync`.
2. Calls `ITicketAccessAuthorizer.EnsureCanMutateAsync`.
3. Mutates the in-memory `Ticket`, producing uncommitted domain events.
4. Applies configured transition validation.
5. Calls `ITicketRepository.SaveAsync` and only then maps a successful
   `TicketView`.

`EfTicketRepository.SaveAsync` loads the persistence row, copies the aggregate
state, and calls its private `Commit` method. When no EF transaction is already
active, `Commit` starts one. For each uncommitted event it adds the event row,
adds assignment data where applicable, acquires the audit-chain lock through
`AuditChainCoordinator`, reads the predecessor hash, and tracks the audit row.
It then calls `SaveChangesAsync`, commits the transaction, and clears the
aggregate's uncommitted events only after commit succeeds.

When an outer transaction already exists, `Commit` joins it and does not commit
itself. The caller owns the final commit. `IncidentService`, `ProblemService`,
and other infrastructure services demonstrate this outer-transaction pattern.

`AuditChainCoordinator` requires an active transaction before inserting or
locking the chain row. The ticket/event/audit writes therefore share the same
transaction when `EfTicketRepository` owns the transaction, and they participate
in the caller's transaction when one is already active.

## Atomicity assessment

The source establishes intended transaction co-location for ticket, event, and
audit writes. It does not establish provider-level rollback or uncertain-commit
behavior. `AuditTransaction` and `EfTicketRepository` rely on `await using`
transaction disposal after `SaveChangesAsync` or `CommitAsync` throws; there is
no explicit rollback call, outcome record, or recovery classification. Provider
behavior after a failed commit is therefore unverified.

The application service also mutates its in-memory aggregate before persistence.
If `SaveAsync` fails, the service propagates the exception and returns no success
view, but the caller-held aggregate may retain the mutation. That is distinct
from database rollback and remains an application-level recovery concern.

## Seam decision

No production seam was added. `ITicketRepository` exposes `SaveAsync` only; the
transaction, audit-chain lock, hash construction, `SaveChangesAsync`, and
commit are private or infrastructure-owned. `AuditChainCoordinator` is injected
into `EfTicketRepository`, but it is not an audit writer abstraction and does
not expose commit/rollback outcomes.

Adding a test-only flag, replacing the repository in production DI, or moving
transaction ownership into the application layer would change the existing
transaction model. That is not a safe change for this sprint.

The smallest future seam is an infrastructure-owned transaction execution
contract used only by `EfTicketRepository` and its integration test fixture.
It should expose begin, commit, and abort outcomes while preserving the current
single transaction around ticket/event/audit writes. The audit writer should
remain inside that boundary; it must not become an independently committed
side effect. Any such seam requires code review and provider-backed validation
for PostgreSQL/Npgsql behavior, including failed commit and failed disposal.

## Failure coverage

Existing database-independent tests in
`TicketLifecycleConfiguredTransitionTests` cover persistence exception
propagation and authorization-before-mutation ordering. They prove no success
result is returned after a fake `SaveAsync` failure and that denied authorization
does not call persistence.

Audit-write failure, transaction commit failure, abort failure, connection loss,
and uncertain commit cannot be injected through the current application
boundary. No test in this sprint claims those properties.

## Validation and preservation

No source code or tests were changed in this sprint. No build or test rerun was
necessary after the read-only review. The previous focused result remains 17
passed tests with a successful zero-warning build, as recorded in
`Docs/PA_TICKETS_STAGE_2_FAILURE_INJECTION_RESULT.md`.

No PostgreSQL or Oracle connection, SQL, migration, DDL, DML, provisioning,
seeding, cleanup, sequence operation, privilege change, or provider-backed test
was performed. RBAC decisions, migrations 026 and 030–038, schema, roadmap
status, branch, and all pre-existing worktree changes were preserved. No reset,
clean, stash, branch switch, commit, or push occurred.

## Remaining Stage 2 criteria and next step

Stage 2 remains `PARTIAL`. Required evidence still includes provider-backed
transaction atomicity, audit-write and commit/abort failure behavior,
uncertain-commit recovery, durable audit-chain verification, and lifecycle API
acceptance.

The next unblocked item is a separately reviewed infrastructure seam design and
provider-independent contract tests for transaction outcomes. Implementation or
provider execution requires explicit owner/provider authorization and must not
weaken the existing transaction or authorization boundaries.
