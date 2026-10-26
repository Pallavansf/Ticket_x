# PA_TICKETS RBAC Persistence Execution Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** narrow PostgreSQL repository persistence proof and RBAC blocker resolution  
**Outcome:** repository persistence is proven; authorized lifecycle/API E2E remains blocked by the unresolved RBAC schema contract.

## Files changed

- `tests/PA.Tickets.Domain.Tests/PostgreSqlTicketPersistenceTests.cs` — added a repository-level provider test that uses the real `EfTicketRepository`, transaction, audit write, fresh-scope reload, and test-owned cleanup. It does not bypass or replace authorization.
- `tests/scripts/postgresql-foundational-ddl.sql` — prior sprint’s source-backed operational metadata, ticket shadow columns, and audit-maintenance support remain in place.

## PostgreSQL proof

Before each state-changing operation, the process verified `localhost:5432`, database `pa_tickets_codex_e2e`, role `pa_tickets_codex`, `transaction_read_only = off`, and `pg_is_in_recovery() = false`. The password came from `PA_TICKETS_E2E_DB_PASSWORD` and was never printed.

The new test created one uniquely identified ticket through `ITicketRepository`, committed it through the real Npgsql/EF path, opened a fresh scope, reloaded it, verified identifier, number, subject, type, status, requester, and the persisted `TICKET_CREATED` audit row, then removed only its own rows using the existing maintenance helper. The test passed against PostgreSQL.

This proves repository persistence and audit insertion. It does not prove authorization, lifecycle service behavior, API behavior, or Angular E2E.

## RBAC evidence and blocker

`DatabaseAuthorizationFactsReader` projects `AdministrationPermissionRow.ModuleCode` from `S_MODULE_CODE`. `AdministrationRows.cs` maps that property explicitly, while `database/migrations/postgresql/031_configurable_rbac.sql` adds and populates the metadata as part of the configurable RBAC model. The approved target’s existing permission table lacks that column, so the lifecycle-service test fails at authorization with PostgreSQL error `42703`.

The source and existing gate documentation establish these facts:

| Decision | Evidence | Safest viable options | Recommended direction | Blocked work |
|---|---|---|---|---|
| Canonical permission catalogue | `AdministrationRows.cs`, authorization services, `031_configurable_rbac.sql`, RBAC gate reports | Approve configurable source catalogue; revive superseded fixed catalogue; defer authorization | Approve the configurable catalogue; do not execute superseded 030 | RBAC migration and all authorization-dependent tests |
| Role/grant semantics | `RoleAuthorizationPolicy.cs`, `AdministrationService.cs`, migration 031 | Preserve existing identities with additive mappings; replace with fixed grants | Preserve existing IDs and add only owner-approved canonical mappings | Role/membership/grant provisioning |
| Scope/delegation | `ScopeAuthorizationPolicy.cs`, `AdminHeadDelegationPolicy.cs`, role-scope/delegation mappings | Explicit authored records; derived records | Require explicit, effective-dated records and fail closed when absent | Cross-scope and delegated API acceptance |
| Historical facts and ledger | T62B decision packet and migration gate resolution | Adopt unknown history as unknown; invent applied rows | Preserve unknown history and require owner-approved ledger representation | Migrations 031–037 sequencing |
| Audit actor contract | migration 038 documentation and audit mappings | Approve nullable/typed actor contract; defer 038 | Keep 038 blocked until actor/FK/nullability contract is approved | Migration 038 |

No permission rows, grants, historical records, ledger entries, role changes, or privileges were created to force the test.

## Validation

Build:

```text
dotnet build tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\roadmap-provider\ -v minimal
```

Result: **succeeded, 0 warnings, 0 errors**.

Focused provider test:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts" -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\roadmap-provider\ -v minimal
```

Result: **1 passed, 0 failed, 0 skipped** against the approved PostgreSQL target.

Database-independent regression selection:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\roadmap-provider\ --filter "FullyQualifiedName!~PostgreSql" -v minimal
```

Result: **291 passed, 0 failed, 0 skipped**.

The full lifecycle service and API/Angular journey were not reported as passed; they remain blocked at the missing RBAC permission metadata. No migrations 026 or 030–038 were executed.

## Next implementation step

Obtain the security/RBAC and database-owner decisions for the configurable permission catalogue, role/grant mappings, historical treatment, migration ledger, and audit actor contract. Then prepare a separately reviewed replacement migration sequence. Until those decisions and execution authorization exist, do not alter the existing permission data or bypass authorization.

All pre-existing modified, untracked, ignored, and local configuration files were preserved. No reset, clean, stash, branch switch, commit, or push was performed.
