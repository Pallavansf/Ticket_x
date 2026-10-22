# PA_TICKETS Real PostgreSQL Integration Result

**Branch:** `workstream-i-lifecycle-authority`  
**Approved target:** `localhost:5432 / pa_tickets_codex_e2e / pa_tickets_codex`  
**Provider:** PostgreSQL 18.6  
**Result:** `BLOCKED` at the repository RBAC migration gate

## Target verification

The password was read from the local `PA_TICKETS_E2E_DB_PASSWORD` variable and
used only through a process-local `PGPASSWORD` value. It was never printed,
logged, committed, or written to a file. The effective connection was checked
before schema work and returned:

- database `pa_tickets_codex_e2e`
- role `pa_tickets_codex`
- server `::1`, port `5432`
- PostgreSQL 18.6
- transaction read-only `off`
- recovery mode `false`

The pre-existing `.env.test.local` generic test variables still describe the
older `pa_tickets_e2e_verify` target. That file was not changed; the approved
codex target was constructed ephemerally from the task values and the password
variable.

## Database operations performed

The initial read-only catalog check found zero public objects. With the same
identity guard immediately before the operation, the checked-in
`tests/scripts/postgresql-foundational-ddl.sql` was applied and committed.

The PostgreSQL versions of migrations 028 and 029 were then applied in order
and committed:

- `database/migrations/postgresql/028_local_password_authentication.sql`
- `database/migrations/postgresql/029_user_profile_hierarchy.sql`

Each operation was preceded by a guard requiring the exact database, role, and
port above.

Migration 030 was attempted only after the same guard, but failed closed at its
own explicit check:

```text
RBAC migration requires the reviewed active permission catalog
```

The migration’s transaction rolled back. A subsequent read-only catalog check
confirmed 71 foundational public objects, the `PA_TICKETS_ROLE` and
`PA_TICKETS_PERMISSION` tables, no RBAC proposal/delegation/ledger objects,
and no permission rows were fabricated.

The file identifies itself as a **superseded draft — do not execute** and
requires 34 reviewed active permission codes. Creating those rows or bypassing
the check would invent business authorization data, so migrations 030 onward
were not continued. Migration 038 is intentionally blocked by its source
contract. Migration 026 and all review candidates remain separate and were not
executed.

No production or protected test database was contacted. No Oracle operation,
application seed, cleanup, sequence reset, privilege change, or migration-026
operation was performed.

## Source validation

```text
dotnet build tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\real-e2e-source\bin\ -v minimal
```

Result: succeeded, 0 warnings, 0 errors.

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~TicketLifecycleTests|FullyQualifiedName~WorkflowApprovalSlaTests|FullyQualifiedName~ReportExportFormatterTests|FullyQualifiedName~ApplicationEffectiveGrantAuthorizationTests" -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\real-e2e-source\bin\ -v minimal
```

Result: 28 passed, 0 failed, 0 skipped. No provider-backed test was included.

## Remaining blocker and required decision

Real API/Angular E2E cannot proceed until an owner-approved PostgreSQL RBAC
baseline supplies the reviewed active permission catalog and resolves the
canonical role/permission, historical-data, migration-ledger, and audit-actor
contracts documented in the existing T62B evidence. A separately approved
migration sequence must then identify which executable migrations are permitted;
030’s superseded draft, 038, and migration 026 must not be bypassed.

## Preservation

The branch remained `workstream-i-lifecycle-authority`. Existing modified,
untracked, and ignored work was preserved. `.env.test.local`, migration files,
application code, and configuration were not modified. No reset, clean, stash,
branch switch, commit, or push was performed.
