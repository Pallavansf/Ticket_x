# PA_TICKETS Stage 2 Slice 50 — PostgreSQL Incident Atomicity Test

## Result

The test-only interceptor and two focused provider cases were implemented, but execution is blocked by the approved database schema: `PA_TICKETS_INCIDENT` does not exist. No schema or migration operation was performed.

## Files changed

- `tests/PA.Tickets.Domain.Tests/PostgreSqlIncidentAtomicityTests.cs` — new provider test fixture.
- `Docs/PA_TICKETS_STAGE_2_SLICE_50_RESULT.md` — this result.

The fixture removes only the test provider's `TicketsDbContext` registration and replaces it with a process-local Npgsql context configured with the test-only `SaveChangesInterceptor`. Production source, runtime configuration, migration files, and database state were unchanged.

## Interceptor and transaction evidence

`ThrowAfterSaveInterceptor` is registered only by the rollback test fixture and throws once from `SavedChangesAsync` callback 2. Callback 1 is the idempotency claim save; callback 2 is the base-ticket save reached through `EfTicketRepository.AddAsync`. The interceptor records whether `Database.CurrentTransaction` is non-null before throwing. The test asserts exactly one injected callback at the intended count and an active transaction. The success test builds the same provider without the interceptor.

Before either case can write, `EnsureIncidentSchemaAsync` performs a read-only `to_regclass` check for `PA_TICKETS_INCIDENT` and fails with an explicit prerequisite error when the table is absent. This prevents cleanup from issuing statements against an unavailable object.

## Target identity

The process-local connection was constructed from `PA_TICKETS_E2E_DB_PASSWORD` in `.env.test.local` without printing or persisting the secret. The previously verified identity matched:

`pa_tickets_codex_e2e / pa_tickets_codex / ::1:5432 / PostgreSQL 18.6 / recovery=false / SSL=false`.

## Commands and actual results

Focused provider cases:

```text
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter FullyQualifiedName~PostgreSqlIncidentAtomicityTests -v minimal
```

Result: **2 failed, 0 passed, 0 skipped**. Both stopped before writes with:

`Incident atomicity provider test requires PA_TICKETS_INCIDENT; no schema changes are authorized by this test.`

The required existing persistence test was then rerun:

```text
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo --filter FullyQualifiedName~Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts -v minimal
```

Result: **1 passed, 0 failed, 0 skipped**. Its unique-record `finally` cleanup completed.

Build:

```text
dotnet build tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --no-restore --nologo
```

Result: **0 warnings, 0 errors**.

## Interpretation and remaining blockers

The interceptor implementation compiles and is designed to exercise the real Npgsql transaction, but rollback and successful incident co-persistence were not observed because the target schema lacks the incident table. No incident rows, events, audits, or cleanup records were created by these cases. Adding the missing table would require an independently authorized schema/migration operation and was not attempted. Stage 2 remains `PARTIAL`; incident rollback, audit durability, concurrency/recovery, and lifecycle API acceptance remain unverified.

`git diff --check` was run; reported whitespace and line-ending notices are pre-existing in other modified documents. No commit or push occurred, and all existing worktree changes were preserved.
