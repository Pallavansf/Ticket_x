# PA_TICKETS Gate T52 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** one unauthorized mutation boundary over hosted HTTP  
**Database/provider activity:** none

## Selected mutation boundary

The selected route is:

`POST /api/v1/tickets/9001/close`

The authenticated user has a valid synthetic `TICKET_UPDATE` grant but only department-20 scope; the ticket belongs to department 10. T45 previously proved a read denial for this mismatch. T52 adds the distinct mutation case, where an unauthorized close attempt must not change ticket state or emit a success audit event.

## Production trace

`TicketsController` is protected by class-level `[Authorize]` and maps the close action to `TicketLifecycleService.CloseAsync` (`src/PA.Tickets.Api/Controllers/TicketsController.cs:3,23`). The real application pipeline performs authentication, password-rotation handling, authorization, and routing before the controller. `CloseAsync` enters the production `Mutate` path, which loads the ticket, calls `ITicketAccessAuthorizer.EnsureCanMutateAsync`, applies the domain close operation, and only then calls `ITicketRepository.SaveAsync` (`src/PA.Tickets.Application/Tickets/TicketLifecycleService.cs:113,116`).

The registered `DatabaseTicketAccessAuthorizer` requests `TICKET_UPDATE` with the ticket resource (`src/PA.Tickets.Infrastructure/Foundations/DatabaseAuthorizationService.cs:221-224`). The real `DatabaseAuthorizationService` evaluates the synthetic effective grant and department scope and rejects the request. `EfTicketRepository.SaveAsync` is the production persistence boundary; its event/audit work is reached only during save (`src/PA.Tickets.Infrastructure/Tickets/EfTicketRepository.cs:9-24`).

## Test and assertions

Added to `tests/PA.Tickets.Domain.Tests/HostedCrossScopeAuthorizationTests.cs`:

`Out_of_scope_ticket_close_is_denied_without_mutation_or_success_audit`

The test uses `WebApplicationFactory<Program>`, the real authorization pipeline, an active non-Spectator fact, an out-of-scope department grant, and a stateful in-memory ticket repository spy. It asserts:

- HTTP `403 Forbidden`.
- The protected ticket subject is absent from the response.
- The global Spectator mutation filter is consulted once and does not deny this non-Spectator actor.
- The authorization-facts reader is invoked once.
- The ticket is loaded for resource authorization.
- `SaveAsync` is never called, so no protected state mutation is attempted.
- Because the success persistence path is never entered, no success audit event is emitted by this denial path.

No denial-audit contract was assumed or asserted. The test proves **no mutation attempted** and **no success-audit event emitted**; it does not claim a denial audit should exist.

## Focused validation

The initial focused run exposed an incorrect expectation that the Spectator filter would not be called for a mutation. The production filter correctly called the non-Spectator reader once; the assertion was corrected to reflect that real path. The same focused command was then rerun:

```powershell
$results = Join-Path $env:TEMP ('pa-tickets-t52-rerun-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
dotnet test tests/PA.Tickets.Domain.Tests/PA.Tickets.Domain.Tests.csproj --filter 'FullyQualifiedName~HostedCrossScopeAuthorizationTests|FullyQualifiedName~HostedRbacAuthorizationTests|FullyQualifiedName~HostedAuthorizationDenialTests|FullyQualifiedName~ApplicationScopeAuthorizationPolicyTests|FullyQualifiedName~EffectiveGrantAuthorizationPolicyTests' --results-directory $results --no-restore --verbosity minimal
```

Final result: **18 passed, 0 failed, 0 skipped**. No full suite or provider-backed test was run.

## Worktree preservation

The branch remained `workstream-i-lifecycle-authority`. Before T52, the worktree status contained 24,163 entries; all existing modified, untracked, and ignored work was preserved. The existing untracked hosted test file was extended with this focused case and this result document was added. Nothing was staged or committed, and no reset, clean, stash, branch switch, or push was performed. Final status contained 24,164 entries.

## Remaining limitations

This verifies hosted authorization ordering and the absence of an attempted save/audit success path using deterministic fakes. It does not prove PostgreSQL/Npgsql transaction behavior, durable audit persistence, audit atomicity, rollback, concurrent mutation, or provider error handling. A provider-backed evidence gate remains necessary for those guarantees. No database, SQL, migration, provisioning, seeding, cleanup, Oracle, or protected-target operation occurred.

