# PA_TICKETS Stage 2 Slice 46 — Lifecycle Creation Atomicity

## Result

The incident-creation transaction-ownership gap was corrected at source level. `IncidentService.CreateAsync` now establishes its transaction before invoking the ticket lifecycle service. `EfTicketRepository.AddAsync` reuses an already active `TicketsDbContext` transaction and does not commit it independently. Stage 2 remains `PARTIAL`; this change does not establish provider-backed atomicity.

## Root cause

Before this slice, `IncidentService.CreateAsync` called `ITicketLifecycleService.CreateAsync` before opening `creationTx`. The lifecycle service called `ITicketRepository.AddAsync`, whose implementation in `src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs` always began and committed a local transaction and cleared ticket events. Incident-specific rows, events, and audit data were then written afterward. A later failure could therefore leave the base ticket committed without its incident row.

## Source changes

- `src/PA.Tickets.Infrastructure/Tickets/IncidentService.cs` (`CreateAsync`): begins an owned transaction before ticket creation; stages the incident row, incident event, and audit record in the same context; saves and commits once; clears deferred ticket events only after a locally owned commit succeeds. A failure discards the pending event reference and propagates the original exception.
- `src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs` (`AddAsync`, `Commit`): detects `db.Database.CurrentTransaction`, reuses a caller-owned transaction, and defers event clearing instead of committing or clearing inside the nested repository call. Repository-owned ordinary ticket creation retains its existing local commit behavior.
- `src/PA.Tickets.Infrastructure/Persistence/TicketEventCommitBoundary.cs`: records deferred aggregates for an outer owner and exposes explicit success/discard operations. Discarding a pending reference never clears events.

The resulting ownership is one owner per path: `IncidentService` owns commit when it starts `creationTx`; an existing outer caller remains responsible for its final commit. No retry is introduced for an uncertain commit outcome, and cleanup exceptions are not used to replace the primary failure.

## Tests

`tests/PA.Tickets.Domain.Tests/TicketEventCommitBoundaryTests.cs` covers the provider-independent boundary contract:

- events remain after a failed/unfinished commit path;
- events clear only at an explicit successful commit boundary;
- caller-owned work defers clearing until the outer owner signals success;
- failed outer commit cleanup discards the pending reference without clearing events.

Commands and results:

```text
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter 'FullyQualifiedName~TicketEventCommitBoundaryTests' -v minimal
Passed: 4, Failed: 0, Skipped: 0

dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TicketEventCommitBoundaryTests|FullyQualifiedName~AuditTransactionOwnershipTests" -v minimal
Passed: 6, Failed: 0, Skipped: 0

dotnet build src/PA.Tickets.Infrastructure/PA.Tickets.Infrastructure.csproj --no-restore --nologo
Build succeeded: 0 warnings, 0 errors
```

These tests prove the event-boundary decisions and ownership signaling in memory. They do not prove PostgreSQL rollback, commit-failure classification, connection-loss behavior, durable co-transactionality, or recovery from an uncertain commit.

## Remaining limitations

The deferred-event registry requires the outer transaction owner to call its success or discard operation. `IncidentService` does this for a transaction it owns; when it is invoked inside a transaction owned by another caller, that caller still needs an explicit completion hook. Provider behavior on disposal/rollback and uncertain commit outcomes remains unverified. Workflow side effects that use separate contexts are outside this narrow correction and require their own transaction evidence.

No database connection, SQL, migration, schema, RBAC, privilege, sequence, provisioning, seed, cleanup, or provider-backed test was executed. Migration 026 and all blocked migrations remain untouched. Existing worktree changes were preserved; no commit or push occurred. `git diff --check` was run after the edits; any pre-existing whitespace/line-ending notices are reported separately from this slice.

## Roadmap status

The source-level incident-creation ownership criterion is advanced, but Stage 2 remains `PARTIAL` pending provider-backed persistence/rollback, audit durability, concurrency, and API acceptance evidence.
