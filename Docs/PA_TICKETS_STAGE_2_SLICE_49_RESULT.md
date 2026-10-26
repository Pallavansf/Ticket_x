# PA_TICKETS Stage 2 Slice 49 — Safe Incident Atomicity Test Design

## Scope

This is a design-only review. No source, test, configuration, schema, or database state was changed, and no provider test was executed.

## Exact failure point and current path

`IncidentService.CreateAsync` (`src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs`) starts `creationTx` before calling `ITicketLifecycleService.CreateAsync`. The lifecycle service reaches `EfTicketRepository.AddAsync`, which detects the active `TicketsDbContext` transaction, stages the base ticket/work items/events/audit rows, and calls `SaveChangesAsync` without committing. Control then returns to `IncidentService`, which stages the incident row, incident event, and incident audit, saves again, commits the transaction it owns, and only then clears deferred ticket events.

The required rollback point is therefore immediately after the first repository `SaveChangesAsync` has completed successfully and before `IncidentService` can stage its incident-specific rows. A failure at that point must leave the transaction uncommitted and cause disposal/rollback to remove the base ticket, ticket event/history, and ticket audit rows as well as the absent incident rows.

## Evaluated test-only approaches

### 1. Replace an existing abstraction

A fake `ITicketLifecycleService` or `ITicketRepository` can throw at the desired point, but a fake would bypass the real EF/Npgsql persistence path. A decorator around the real repository or lifecycle service could preserve the provider path, but the current dependency-injection registrations do not expose a simple decorator hook; implementing one would require substantial test-only forwarding code and careful registration ordering. This option is possible but unnecessarily invasive.

### 2. Test-only EF Core interceptor — recommended

Register a `SaveChangesInterceptor` only in the test's service provider. Its `SavedChangesAsync` callback should throw once, after the first successful `SaveChangesAsync` for the incident creation context. The first save is the repository's base-ticket persistence while `creationTx` is active; the exception is raised before the incident-specific save and before commit. The interceptor must be disabled for the success case and scoped to the test context/operation so it cannot affect other tests.

This approach exercises the real Npgsql connection, EF change tracking, transaction, repository, audit writer, and rollback path. It does not require a schema object, trigger, production flag, environment switch, or runtime behavior change. The test fixture must verify that the interceptor is attached to the `TicketsDbContext` actually injected into `IncidentService`; merely registering an unused interceptor is insufficient.

### 3. Internal test seam — fallback only

An internal, test-only callback around the first repository save could make the point explicit, but it would require production source changes and a new seam in the transaction path. It should be considered only if interceptor registration cannot be proven to target the live context. No such seam should be added for this design slice.

## Recommended test shape

Add a provider test fixture that builds the normal PostgreSQL services and adds a one-shot interceptor exclusively to the test `TicketsDbContext`. Use a unique subject/idempotency key containing a new GUID. Run:

1. **Rollback case:** call `IIncidentService.CreateAsync`; expect the injected exception. In a fresh context, query by the unique subject and assert no ticket exists. If a row is unexpectedly present, inspect only that exact test-owned row and its related incident, event, history, and audit records, then clean those exact records in `finally`.
2. **Success case:** run the same normal service without the interceptor using a second unique key. Assert one ticket and incident row, the `INCIDENT_CREATED` event, ticket history, and incident/ticket audit records. Verify the persisted ticket can be reloaded in a fresh scope.
3. **Ownership assertion:** the interceptor must observe the first save while `Database.CurrentTransaction` is non-null; the test must not commit or begin another transaction. The service's own commit is the only commit in the local-owner path.

The rollback test must not rely on a known ticket ID because the exception occurs before `CreateAsync` returns. The unique subject is the lookup boundary; cleanup is allowed only for an exact matching fixture identifier and must not use broad truncation or shared-data deletion.

## Expected assertions and risks

The provider must confirm that the thrown post-save exception leads to a confirmed rollback and that no participating rows remain. A fake or in-memory test cannot establish this. If the provider reports an uncertain commit/disposal outcome, the test must stop without retrying and classify the database state as unknown.

The existing `PostgreSqlIncidentTests` already use unique-ish fixture data but also perform reference-data writes and broad fixed identifiers, so they should not be reused unchanged for this atomicity proof. The new fixture must avoid reference-data mutation unless independently required and authorized.

## Prerequisites and next action

- The approved target identity and process-local secret configuration must remain verified before the test.
- No migration, schema, grant, privilege, sequence, seed, or migration-026 operation is permitted.
- The interceptor registration and one-shot behavior must be reviewed in the test diff before execution.
- Provider-level rollback, audit durability, and uncertain-commit behavior remain unverified.

Implement the interceptor-backed test only after this design is accepted and the fixture registration is confirmed. Stage 2 remains `PARTIAL`; no formal approval status changes were made.
