# PA_TICKETS Ticket Journey Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** filtered ticket search to authorized detail/history presentation

## User-visible capability

Ticket detail now presents persisted lifecycle history alongside the existing
summary and work-item information. The history is returned by the existing
authorized ticket detail use case, ordered by event timestamp and event ID,
and includes action, category, actor, old/new values, reason, and timestamp.
The Angular detail page renders the history and still preserves the complete
ticket-list filter context when returning to search.

The prior ticket-list increment was reviewed and retained: all API-supported
numeric filters are exposed, serialized into query parameters, restored from
the URL, and forwarded as numeric search criteria. No regression was found.

## Source path

`TicketDetailPage` calls `TicketsApiService.get`, which calls
`GET /api/v1/tickets/{id}`. `TicketsController` delegates to
`TicketLifecycleService.GetAsync`; authorization is checked before the new
history query. `EfTicketRepository.GetHistoryAsync` reads only the ticket's
persisted `PA_TICKETS_EVENT` rows in deterministic order. The response adds an
optional `history` collection to `TicketView`, preserving existing callers and
mutation responses.

This proves the source-to-repository path and the UI presentation. Full API
and browser acceptance remains blocked by the unresolved persisted RBAC
catalogue (`S_MODULE_CODE`).

## Files changed

- `src/PA.Tickets.Application/Tickets/TicketCommands.cs`
- `src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs`
- `src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs`
- `tests/PA.Tickets.Domain.Tests/PostgreSqlTicketPersistenceTests.cs`
- `client/src/app/core/tickets-api.service.ts`
- `client/src/app/features/tickets/ticket-detail.page.ts`
- `client/src/app/features/tickets/ticket-detail.html`
- `client/src/app/features/tickets/ticket-detail.page.spec.ts`
- Existing filter files remain changed from the prior sprint.
- `Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md` retains the source-to-schema RBAC decision packet.

## Validation

Backend build:

```text
dotnet build tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-restore --nologo --disable-build-servers -m:1 -p:BuildInParallel=false -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\journey-history\ -v minimal
```

Result: **succeeded, 0 warnings, 0 errors**.

Database-independent regression selection:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\journey-history\ --filter "FullyQualifiedName!~PostgreSql" -v minimal
```

Result: **291 passed, 0 failed, 0 skipped**.

Provider repository test, using the established write guard and test-owned
cleanup:

```text
dotnet test tests\PA.Tickets.Domain.Tests\PA.Tickets.Domain.Tests.csproj --no-build --no-restore --nologo --disable-build-servers --filter "FullyQualifiedName~Ef_ticket_repository_persists_and_reloads_a_ticket_without_authorization_facts" -p:BaseOutputPath=G:\WSS\aw\Tickets\temp\journey-history\ -v minimal
```

Result: **1 passed, 0 failed, 0 skipped**. The verified identity was
`pa_tickets_codex_e2e|pa_tickets_codex|5432|off|f` (database, role, port,
read-only setting, recovery state). The fixture and its audit/event rows were
cleaned by the test.

Focused Angular tests:

```text
npm.cmd test -- --watch=false --include=src/app/features/tickets/ticket-detail.page.spec.ts --include=src/app/features/tickets/ticket-list.page.spec.ts
```

Result: **10 passed**.

Full Angular suite with a 60-second timeout:

```text
npm.cmd test -- --watch=false
```

Result: **13 test files, 55 tests passed**.

Angular production build:

```text
npm.cmd run build -- --configuration production
```

Result: succeeded with the existing stylesheet budget warning in
`role-administration.page.scss`.

No migrations, schema changes, RBAC rows, grants, privilege changes, or
Oracle operations were performed in this sprint.

## Remaining blocker and next step

The API lifecycle path still fails at the unresolved authorization schema:
`DatabaseAuthorizationFactsReader` requires `PA_TICKETS_PERMISSION.S_MODULE_CODE`,
which is absent from the approved catalog. The security/RBAC and database
owners must approve the configurable permission catalogue, five-role grant
semantics, historical effective dates, scope/delegation lineage, migration
ledger treatment, and audit-actor contract before migrations 030–038 or API
acceptance can proceed. Migration 026 remains a separate approval chain.

No roadmap stage is marked complete. The next concrete step is owner-approved
RBAC schema/data reconciliation followed by a separately authorized API
journey test.

All existing worktree changes, ignored files, and `.env.test.local` were
preserved. No reset, clean, stash, branch switch, commit, or push was used.
